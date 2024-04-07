using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Color = System.Drawing.Color;

namespace ImageParallel
{
    public class TaskData
    {
        public Bitmap Image { get; set; }
        public int I { get; set; }

        public TaskData(Bitmap image, int i)
        {
            Image = image;
            I = i;
        }
    }
    public partial class MainWindow : Window
    {
        private int _parallelProcessNum = 8;
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
                Stopwatch stopwatch = Stopwatch.StartNew();
                transformation(modifiedImage);
                stopwatch.Stop();

                ModifiedImage.Source = ConvertBitmapToBitmapImage(modifiedImage);

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
            Stopwatch stopwatch = Stopwatch.StartNew();
            ApplyNegative(image);
            stopwatch.Stop();
            Debug.WriteLine($"Sequentially negative -{stopwatch.ElapsedMilliseconds}");

            stopwatch = Stopwatch.StartNew();
            ApplyHorizontalSymmetry(image);
            stopwatch.Stop();
            Debug.WriteLine($"Sequentially horizontal symmetry - " + stopwatch.ElapsedMilliseconds);

            stopwatch = Stopwatch.StartNew();
            ApplyCyclicShift(image);
            stopwatch.Stop();
            Debug.WriteLine($"Sequentially cyclic shift - " + stopwatch.ElapsedMilliseconds);
        }

        private void ProcessImageParallelFor(Bitmap image)
        {
            var step = image.Width / _parallelProcessNum;
            var height = image.Height;

            var list = new List<Bitmap>();

            for (var i = 0; i < _parallelProcessNum; i++)
            {
                var rect = new Rectangle(i * step, 0, step, height);
                list.Add(image.Clone(rect, image.PixelFormat));
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            Parallel.For(0, _parallelProcessNum, i =>
            {
                try
                {
                    ApplyNegative(list[i]);
                } catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            });
            stopwatch.Stop();
            Debug.WriteLine($"ParallelFor negative - " + stopwatch.ElapsedMilliseconds);

            stopwatch = Stopwatch.StartNew();
            Parallel.For(0, _parallelProcessNum, i =>
            {
                try
                {
                    ApplyHorizontalSymmetry(list[i]);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            });
            stopwatch.Stop();
            Debug.WriteLine($"ParallelFor horizontal symmetry - " + stopwatch.ElapsedMilliseconds);

            stopwatch = Stopwatch.StartNew();
            Parallel.For(0, _parallelProcessNum, i =>
            {
                try
                {
                    ApplyCyclicShift(list[i]);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            });
            stopwatch.Stop();
            Debug.WriteLine($"ParallelFor cyclic shift - " + stopwatch.ElapsedMilliseconds);

            for (var i = 0; i < _parallelProcessNum; i++)
            {
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < step; x++)
                    {
                        image.SetPixel(x + i * step, y, list[i].GetPixel(x, y));
                    }
                }

                list[i].Dispose();
            }

        }

        private void ProcessImageWithTasks(Bitmap image)
        {
            var step = image.Width / _parallelProcessNum;
            var height = image.Height;

            var list = new List<Bitmap>();

            for (var i = 0; i < _parallelProcessNum; i++)
            {
                var rect = new Rectangle(i * step, 0, step, height);
                list.Add(image.Clone(rect, image.PixelFormat));
            }

            var tasks = new Task[_parallelProcessNum];

            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = new Task((object obj) =>
                {
                    var data = (TaskData)obj;
                    try
                    {
                        ApplyNegative(data.Image);
                        list[data.I] = data.Image;

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }, new TaskData(list[i], i));

                tasks[i].Start();

            }

            Task.WaitAll(tasks);
            stopwatch.Stop();
            Debug.WriteLine($"Tasks negative - " + stopwatch.ElapsedMilliseconds);

            tasks = new Task[_parallelProcessNum];

            stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = new Task((object obj) =>
                {
                    var data = (TaskData)obj;
                    try
                    {
                        ApplyHorizontalSymmetry(data.Image);
                        list[data.I] = data.Image;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }, new TaskData(list[i], i));

                tasks[i].Start();

            }

            Task.WaitAll(tasks);
            stopwatch.Stop();
            Debug.WriteLine($"Tasks horizontal symmetry - " + stopwatch.ElapsedMilliseconds);

            tasks = new Task[_parallelProcessNum];

            stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = new Task((object obj) =>
                {
                    var data = (TaskData)obj;
                    try
                    {
                        ApplyCyclicShift(data.Image);
                        list[data.I] = data.Image;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }, new TaskData(list[i], i));

                tasks[i].Start();

            }

            Task.WaitAll(tasks);
            stopwatch.Stop();
            Debug.WriteLine("Tasks shift - " + stopwatch.ElapsedMilliseconds);

            for (var i = 0; i < _parallelProcessNum; i++)
            {
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < step; x++)
                    {
                        image.SetPixel(x + i * step, y, list[i].GetPixel(x, y));
                    }
                }

                list[i].Dispose();
            }
        }

        private void ApplyNegative(Bitmap image)
        {
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var pixel = image.GetPixel(x, y);
                    image.SetPixel(x, y, Color.FromArgb(255, 255 - pixel.R, 255 - pixel.G, 255 - pixel.B));
                }
            }
        }

        private void ApplyHorizontalSymmetry(Bitmap image)
        {
            for (int y = 0; y < (image.Height / 2); y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var pixel1 = image.GetPixel(x, y);
                    var pixel2 = image.GetPixel(x, image.Height - 1 - y);
                    image.SetPixel(x, y, pixel2);
                    image.SetPixel(x, image.Height - 1 - y, pixel1);
                }
            }
        }

        private void ApplyCyclicShift(Bitmap image)
        {
            var shift = _shift % image.Height;
            var temp = new Bitmap(image);

            for (int x = 0; x < image.Width; x++)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    int newY = (y + shift) % image.Height;
                    if (newY < 0)
                        newY += image.Height;

                    var color = temp.GetPixel(x, y);
                    image.SetPixel(x, newY, color);
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