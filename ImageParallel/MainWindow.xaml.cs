using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Color = System.Drawing.Color;

namespace ImageParallel
{
    public partial class MainWindow : Window
    {
        private int _parallelProcessNum = 16;
        private int _shift = 50;
        private Bitmap originalImage;

        private string imagesSaveDirectory;
        private string logFilePath; 

        public MainWindow()
        {
            InitializeComponent();

            imagesSaveDirectory = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "SavedImages");
            logFilePath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "image_transformations_log.csv");

            Directory.CreateDirectory(imagesSaveDirectory); 

            if (!File.Exists(logFilePath))
            {
                File.WriteAllText(logFilePath, "Timestamp;Image Size;Transformation;Duration (ms);Saved File Path\n");
            }
        }

        private void LoadImageButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png, *.bmp) | *.jpg; *.jpeg; *.png; *.bmp";

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                originalImage = new Bitmap(filePath);

                OriginalImage.Source = ConvertBitmapToBitmapImage(originalImage);
            }
        }

        private void ApplySequentialButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyTransformation(ProcessImageSequentially);
        }

        private void ApplyParallelForButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyTransformation(ProcessImageParallelFor);
        }

        private void ApplyTaskButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyTransformation(ProcessImageWithTasks);
        }

        private void ApplyTransformation(Action<Bitmap> transformation)
        {
            if (originalImage != null)
            {
                Bitmap modifiedImage = new Bitmap(originalImage);
                Stopwatch stopwatch = Stopwatch.StartNew(); // Start timing
                transformation(modifiedImage);
                stopwatch.Stop(); // Stop timing

                ModifiedImage.Source = ConvertBitmapToBitmapImage(modifiedImage);

                // Log results and save the image
                string transformationName = transformation.Method.Name.Replace("ProcessImage", "");
                SaveImageAndLogResults(modifiedImage, transformationName, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                MessageBox.Show("Please load an image first.");
            }
        }

        private void ProcessImageSequentially(Bitmap image)
        {
            ApplyNegative(image, 0, image.Width , image.Height);
            ApplyHorizontalSymmetry(image, 0, image.Width, image.Height);
            ApplyCyclicShift(image, _shift, 0, image.Width, image.Height);
        }

        private void ProcessImageParallelFor(Bitmap image)
        {
            var step = image.Width / _parallelProcessNum;
            var height = image.Height;
            Parallel.For(0, _parallelProcessNum, i =>
            {
                try
                {
                    ApplyNegative(image, i * step, (i + 1) * step, height);
                } catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            });
            Parallel.For(0, _parallelProcessNum, i =>
            {
                try
                {
                    ApplyHorizontalSymmetry(image, i * step, (i + 1) * step, height);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            });
            Parallel.For(0, _parallelProcessNum, i =>
            {
                try
                {
                    ApplyCyclicShift(image, _shift, i * step, (i + 1) * step, height);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            });

        }

        private void ProcessImageWithTasks(Bitmap image)
        {
            var step = image.Width / _parallelProcessNum;
            var height = image.Height;
            var tasks = new Task[_parallelProcessNum];

            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = new Task((object obj) =>
                {
                    var j = (int)obj;
                    try
                    {
                        ApplyNegative(image, j * step, (j + 1) * step, height);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }, i);

                tasks[i].Start();

            }

            Task.WaitAll(tasks);

            tasks = new Task[_parallelProcessNum];

            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = new Task((object obj) =>
                {
                    var j = (int)obj;
                    try
                    {
                        ApplyHorizontalSymmetry(image, j * step, (j + 1) * step, height);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }, i);

                tasks[i].Start();

            }

            Task.WaitAll(tasks);

            tasks = new Task[_parallelProcessNum];

            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = new Task((object obj) =>
                {
                    var j = (int)obj;
                    try
                    {
                        ApplyCyclicShift(image,_shift, j * step, (j + 1) * step, height);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }, i);

                tasks[i].Start();

            }

            Task.WaitAll(tasks);
        }

        private void ApplyNegative(Bitmap image, int startX, int endX, int height)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    lock (image)
                    {
                        var pixel = image.GetPixel(x, y);
                        image.SetPixel(x, y, Color.FromArgb(255, 255 - pixel.R, 255 - pixel.G, 255 - pixel.B));
                    }
                }
            }
        }

        private void ApplyHorizontalSymmetry(Bitmap image, int startX, int endX, int height)
        {
            for (int y = 0; y < (height / 2); y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    lock (image)
                    {
                        var pixel1 = image.GetPixel(x, y);
                        var pixel2 = image.GetPixel(x, height - 1 - y);
                        image.SetPixel(x, y, pixel2);
                        image.SetPixel(x, height - 1 - y, pixel1);
                    }
                }
            }
        }

        private void ApplyCyclicShift(Bitmap image, int shift, int startX, int endX, int height)
        {
            shift = shift % image.Width;
            Bitmap temp = new Bitmap(image);

            for (int y = 0; y < height; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    lock (image)
                    {
                        int newX = (x + shift) % image.Width;
                        if (newX < 0)
                            newX += image.Width;

                        Color color = temp.GetPixel(x, y);
                        image.SetPixel(newX, y, color);
                    }
                }
            }
        }

        private BitmapImage ConvertBitmapToBitmapImage(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                return bitmapImage;
            }
        }

        private void SaveImageAndLogResults(Bitmap image, string transformationType, long duration)
        {
            string fileName = $"{transformationType}_{DateTime.Now:yyyyMMddHHmmss}.bmp";
            string filePath = System.IO.Path.Combine(imagesSaveDirectory, fileName);

            image.Save(filePath);

            LogResults(image.Width, image.Height, transformationType, duration, filePath);
        }

        private void LogResults(int width, int height, string transformationType, long duration, string filePath)
        {
            string logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss};{width}x{height};{transformationType};{duration};{filePath}\n";
            File.AppendAllText(logFilePath, logLine);
        }
    }
}