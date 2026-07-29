
using FractalSharp.Algorithms;
using FractalSharp.Imaging;
using FractalSharp.Processing;
using GoFractal.Common;
using SkiaSharp;
using System.CommandLine;

namespace GoFractal.Compute
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
        private static readonly CancellationTokenSource cts = new CancellationTokenSource();

        static async Task Main(string[] args)
        {
            Console.CancelKeyPress += OnCancelKeyPress;

            RootCommand rootCommand = new RootCommand("GoFractal.Compute - Lightning fast companion app to GoFractal for high resolution, customized fractal rendering.");

            Argument<FileInfo[]> configFilesArgument = new Argument<FileInfo[]>("config-files")
            {
                Arity = ArgumentArity.OneOrMore,
                Description = "List of one or more GoFractal JSON files to render"
            };
            configFilesArgument.AcceptExistingOnly();
            rootCommand.Add(configFilesArgument);

            Option<int> imageWidthOption = new Option<int>("--image-width")
            {
                DefaultValueFactory = r => 3840,
                Description = "Width of the output image in pixels"
            };
            rootCommand.Add(imageWidthOption);

            Option<int> imageHeightOption = new Option<int>("--image-height")
            {
                DefaultValueFactory = r => 2160,
                Description = "Height of the output image in pixels"
            };
            rootCommand.Add(imageHeightOption);

            Option<int> numThreadsOption = new Option<int>("--num-threads")
            {
                DefaultValueFactory = r => Environment.ProcessorCount,
                Description = "Number of threads to use for image rendering"
            };
            rootCommand.Add(numThreadsOption);

            rootCommand.SetAction(async Task (r, ct) =>
            {
                Console.WriteLine("Process started.");

                foreach (string configFilePath in r.GetRequiredValue(configFilesArgument).Select(x => x.FullName))
                {
                    if (ct.IsCancellationRequested) break;

                    string imageFilePath = Path.ChangeExtension(configFilePath, ".png");

                    int imageWidth = r.GetRequiredValue(imageWidthOption);
                    int imageHeight = r.GetRequiredValue(imageHeightOption);

                    int numThreads = r.GetRequiredValue(numThreadsOption);

                    bool frameFinished = false;
                    try
                    {
                        RenderConfig config = RenderConfig.FromJson(File.ReadAllText(configFilePath));
                        Console.WriteLine($"Found configuration named \"{config.Name}\" in \"{Path.GetFileName(configFilePath)}\".");

                        Console.WriteLine($"Computing raw fractal data...");
                        IProcessor<PointData<double>> fractalProcessor = Utilities.CreateFractalProcessor(typeof(GPUFractalProcessor<,,>), config.FractalType, imageWidth, imageHeight);

                        PointData<double>[,] inputData;
                        using (fractalProcessor as IDisposable)
                        {
                            await fractalProcessor.SetupAsync(new ProcessorConfig
                            {
                                ThreadCount = numThreads,
                                Params = config.FractalParams,
                            }, ct);
                            inputData = await fractalProcessor.ProcessAsync(ct);
                        }

                        Console.WriteLine("Computing colors for inner points...");
                        IProcessor<double> innerColorProcessor = Utilities.CreateColorProcessor(config.InnerColorType, imageWidth, imageHeight);

                        double[,] innerIndicies;
                        using (innerColorProcessor as IDisposable)
                        {
                            await innerColorProcessor.SetupAsync(new ColorProcessorConfig
                            {
                                ThreadCount = numThreads,
                                Params = config.InnerColorParams,

                                PointClass = PointClass.Inner,
                                InputData = inputData
                            }, ct);
                            innerIndicies = await innerColorProcessor.ProcessAsync(ct);
                        }

                        Console.WriteLine("Computing colors for outer points...");
                        IProcessor<double> outerColorProcessor = Utilities.CreateColorProcessor(config.OuterColorType, imageWidth, imageHeight);

                        double[,] outerIndicies;
                        using (outerColorProcessor as IDisposable)
                        {
                            await outerColorProcessor.SetupAsync(new ColorProcessorConfig
                            {
                                ThreadCount = numThreads,
                                Params = config.OuterColorParams,

                                PointClass = PointClass.Outer,
                                InputData = inputData
                            }, ct);
                            outerIndicies = await outerColorProcessor.ProcessAsync(ct);
                        }

                        Console.WriteLine("Building image...");

                        SKBitmap bitmap;
                        using (SkiaImageBuilder imager = new SkiaImageBuilder())
                        {
                            imager.CreateImage(outerIndicies, innerIndicies, config.OuterGradient, config.InnerGradient);
                            bitmap = imager.Bitmap!.Copy();
                        }

                        Console.WriteLine($"Writing \"{Path.GetFileName(imageFilePath)}\" next to JSON configuration file...");
                        using (SKWStream outputStream = new SKFileWStream(imageFilePath))
                        {
                            bitmap.Encode(outputStream, SKEncodedImageFormat.Png, 100);
                        }

                        Console.WriteLine("Image rendered successfully!");
                        frameFinished = true;
                    }
                    catch (AggregateException ex) when (ex.InnerExceptions.Any(err => err.GetType() == typeof(OperationCanceledException))) { }
                    catch (OperationCanceledException) { }

                    if (!frameFinished)
                    {
                        File.Delete(imageFilePath);
                    }
                }

                Console.WriteLine("Process halted. All remaining tasks completed gracefully!");
            });

            await rootCommand.Parse(args).InvokeAsync(cancellationToken: cts.Token);
        }

        private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            Console.Error.WriteLine("Cancellation registered! Waiting for current processing to complete...");
            cts.Cancel();
            e.Cancel = true;
        }
    }
}