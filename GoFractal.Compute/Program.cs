/*
 *  Copyright 2018-2026 Chosen Few Software
 *  This file is part of FractalSharp.
 *
 *  FractalSharp is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU Lesser General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  FractalSharp is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU Lesser General Public License for more details.
 *
 *  You should have received a copy of the GNU Lesser General Public License
 *  along with FractalSharp.  If not, see <https://www.gnu.org/licenses/>.
 */

using FractalSharp.Algorithms;
using FractalSharp.Imaging;
using FractalSharp.Processing;
using GoFractal.Common;
using QuadrupleLib;
using SkiaSharp;

using TAccelerator = QuadrupleLib.Accelerators.SoftwareAccelerator;

namespace FractalSharp.ExampleApp
{
    unsafe class SkiaImageBuilder : UpscalingImageBuilder, IDisposable
    {
        public SKBitmap? Bitmap { get; private set; }

        private bool disposedValue;

        public override void InitializeImage(int width, int height)
        {
            Bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        }

        public override void WritePixel(int x, int y, RgbaValue color)
        {
            byte* ptr = (byte*)
                (Bitmap?.GetPixels() + Bitmap?.RowBytes * y + x * 4)
                .GetValueOrDefault()
                .ToPointer();
            *ptr++ = color.Red;
            *ptr++ = color.Green;
            *ptr++ = color.Blue;
            *ptr++ = color.Alpha;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Bitmap?.Dispose();
                }

                Bitmap = null;

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    class Program
    {
        private const int WIDTH = 2560 * 4;
        private const int HEIGHT = 1440 * 4;

        private static readonly SkiaImageBuilder Imager = new SkiaImageBuilder();

        private static readonly Gradient Colors =
            new Gradient(256, new List<GradientKey>
            {
                new GradientKey(new RgbaValue(0, 0, 0)),
                new GradientKey(new RgbaValue(9, 1, 47)),
                new GradientKey(new RgbaValue(4, 4, 73)),
                new GradientKey(new RgbaValue(0, 7, 100)),
                new GradientKey(new RgbaValue(12, 44, 138)),
                new GradientKey(new RgbaValue(24, 82, 177)),
                new GradientKey(new RgbaValue(57, 125, 209)),
                new GradientKey(new RgbaValue(134, 181, 229)),
                new GradientKey(new RgbaValue(211, 236, 248)),
                new GradientKey(new RgbaValue(241, 233, 191)),
                new GradientKey(new RgbaValue(248, 201, 95)),
                new GradientKey(new RgbaValue(255, 170, 0)),
                new GradientKey(new RgbaValue(204, 128, 0)),
                new GradientKey(new RgbaValue(153, 87, 0)),
                new GradientKey(new RgbaValue(106, 52, 3)),
                new GradientKey(new RgbaValue(66, 30, 15)),
                new GradientKey(new RgbaValue(25, 7, 26))
            });

        private static readonly CancellationTokenSource cts = new CancellationTokenSource();

        static async Task Main(string[] args)
        {
            Console.WriteLine("Process started.");

            Console.CancelKeyPress += OnCancelKeyPress;

            RenderConfig config = RenderConfig.FromJson(File.ReadAllText(args[0]));

            IProcessor<PointData<double>> fractalProcessor = Utilities.CreateFractalProcessor(typeof(GPUFractalProcessor<,,>), config.FractalType, WIDTH, HEIGHT);
            IProcessor<double> innerColorProcessor = Utilities.CreateColorProcessor(config.InnerColorType, WIDTH, HEIGHT);
            IProcessor<double> outerColorProcessor = Utilities.CreateColorProcessor(config.OuterColorType, WIDTH, HEIGHT);

            // touch file to allocate it (effectively lock the file)
            using SKWStream outputStream = new SKFileWStream(Path.ChangeExtension(args[0], ".png"));
            bool frameFinished = false;

            try
            {
                Console.WriteLine($"Computing raw fractal data...");
                await fractalProcessor.SetupAsync(new ProcessorConfig
                {
                    ThreadCount = Environment.ProcessorCount,
                    Params = config.FractalParams,
                }, cts.Token);
                PointData<double>[,] inputData = await fractalProcessor.ProcessAsync(cts.Token);

                Console.WriteLine("Computing colors for inner points...");
                await innerColorProcessor.SetupAsync(new ColorProcessorConfig
                {
                    ThreadCount = Environment.ProcessorCount,

                    Params = config.InnerColorParams,
                    PointClass = PointClass.Inner,

                    InputData = inputData
                }, cts.Token);
                double[,] innerIndicies = await innerColorProcessor.ProcessAsync(cts.Token);

                Console.WriteLine("Computing colors for outer points...");
                await outerColorProcessor.SetupAsync(new ColorProcessorConfig
                {
                    ThreadCount = Environment.ProcessorCount,

                    Params = config.OuterColorParams,
                    PointClass = PointClass.Outer,

                    InputData = inputData
                }, cts.Token);
                double[,] outerIndicies = await outerColorProcessor.ProcessAsync(cts.Token);

                Console.WriteLine("Building image...");
                Imager.CreateImage(outerIndicies, innerIndicies, Colors, Colors);

                Console.WriteLine("Writing image file to disk...");
                Imager.Bitmap?.Encode(outputStream, SKEncodedImageFormat.Png, 100);

                Console.WriteLine("Image rendered successfully!");
                frameFinished = true;
            }
            catch (AggregateException ex) when (ex.InnerExceptions.Any(err => err.GetType() == typeof(OperationCanceledException))) { }
            catch (OperationCanceledException) { }
            cts.Dispose();

            if (!frameFinished)
            {
                File.Delete(Path.ChangeExtension(args[0], ".png"));
            }

            Imager.Dispose();

            Console.WriteLine("Process halted. All remaining tasks completed gracefully!");
        }

        private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("Cancellation registered! Waiting for current processing to complete...");
            cts.Cancel();
            e.Cancel = true;
        }
    }
}