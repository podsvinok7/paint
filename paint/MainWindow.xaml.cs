using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.Collections.Generic;

namespace paint
{
    public partial class MainWindow : Window
    {
        enum Tool { Pencil, Line, Rectangle, Ellipse, Eraser }
        Tool currentTool = Tool.Pencil;
        Shape currentShape;
        Polyline currentLine;
        List<Point> points = new List<Point>();
        bool isDrawing;
        Color currentColor = Colors.Black;
        double brushSize = 1;
        double canvasAngle = 0;
        private WriteableBitmap originalCanvasBitmap = null;
        private bool filterActive = false;


        public MainWindow()
        {
            InitializeComponent();
            brushSizeSlider.ValueChanged += BrushSizeSlider_ValueChanged;
            InitializeColorPalette();
            UpdateStatus();
        }

        void InitializeColorPalette()
        {
            colorPalette.ItemsSource = new Color[] { Colors.Black, Colors.White, Colors.Red, Colors.Green, Colors.Blue, Colors.Yellow };
        }

        private void RotateCanvas_Click(object sender, RoutedEventArgs e)
        {
            canvasAngle = (canvasAngle + 90) % 360;
            drawingCanvas.RenderTransform = CreateTransform(canvasAngle);
        }

        private Transform CreateTransform(double angle)
        {
            FrameworkElement parent = drawingCanvas.Parent as FrameworkElement;
            double w = drawingCanvas.ActualWidth, h = drawingCanvas.ActualHeight;
            double rotatedWidth = (angle % 180 == 0) ? w : h;
            double rotatedHeight = (angle % 180 == 0) ? h : w;
            double scaleX = parent.ActualWidth / rotatedWidth;
            double scaleY = parent.ActualHeight / rotatedHeight;
            double scale = scaleX < scaleY ? scaleX : scaleY;
            TransformGroup tg = new TransformGroup();
            tg.Children.Add(new ScaleTransform(scale, scale, w / 2, h / 2));
            tg.Children.Add(new RotateTransform(angle, w / 2, h / 2));
            return tg;
        }

        private void ToolComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (toolComboBox.SelectedIndex >= 0)
            {
                currentTool = (Tool)toolComboBox.SelectedIndex;
                UpdateStatus();
            }
        }
        private void ColorRectangle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Rectangle r = sender as Rectangle;
            if (r != null && r.Fill is SolidColorBrush)
                currentColor = ((SolidColorBrush)r.Fill).Color;
        }

        private void DrawingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            undoStack.Push(GetCanvasBitmap());
            Point p = e.GetPosition(drawingCanvas);
            isDrawing = true;
            if (currentTool == Tool.Pencil)
            {
                currentLine = new Polyline { Stroke = new SolidColorBrush(currentColor), StrokeThickness = brushSize, StrokeLineJoin = PenLineJoin.Round };
                points.Clear(); points.Add(p); currentLine.Points = new PointCollection(points); drawingCanvas.Children.Add(currentLine);
            }
            else if (currentTool == Tool.Eraser) RemoveAtPoint(p);
            else CreateShape(p);
        }

        private void DrawingCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point p = e.GetPosition(drawingCanvas);
            coordinatesText.Text = "X: " + (int)p.X + ", Y: " + (int)p.Y;
            if (!isDrawing) return;
            if (currentTool == Tool.Pencil && currentLine != null) { points.Add(p); currentLine.Points = new PointCollection(points); }
            else if (currentTool == Tool.Eraser) RemoveAtPoint(p);
            else UpdateShape(p);
        }

        private void DrawingCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDrawing = false;
            currentLine = null; points.Clear(); currentShape = null;
        }

        private void CreateShape(Point start)
        {
            if (currentTool == Tool.Line)
            {
                currentShape = new Line { Stroke = new SolidColorBrush(currentColor), StrokeThickness = brushSize, X1 = start.X, Y1 = start.Y, X2 = start.X, Y2 = start.Y };
            }
            else if (currentTool == Tool.Rectangle)
            {
                currentShape = new Rectangle { Stroke = new SolidColorBrush(currentColor), StrokeThickness = brushSize };
                Canvas.SetLeft(currentShape, start.X); Canvas.SetTop(currentShape, start.Y);
            }
            else if (currentTool == Tool.Ellipse)
            {
                currentShape = new Ellipse { Stroke = new SolidColorBrush(currentColor), StrokeThickness = brushSize };
                Canvas.SetLeft(currentShape, start.X); Canvas.SetTop(currentShape, start.Y);
            }
            drawingCanvas.Children.Add(currentShape);
        }

        private void UpdateShape(Point curr)
        {
            if (currentShape is Line)
            {
                Line l = (Line)currentShape;
                l.X2 = curr.X; l.Y2 = curr.Y;
            }
            else if (currentShape is Rectangle)
            {
                Rectangle r = (Rectangle)currentShape;
                double left = Canvas.GetLeft(r), top = Canvas.GetTop(r);
                r.Width = Math.Abs(curr.X - left);
                r.Height = Math.Abs(curr.Y - top);
                Canvas.SetLeft(r, curr.X < left ? curr.X : left);
                Canvas.SetTop(r, curr.Y < top ? curr.Y : top);
            }
            else if (currentShape is Ellipse)
            {
                Ellipse el = (Ellipse)currentShape;
                double left = Canvas.GetLeft(el), top = Canvas.GetTop(el);
                el.Width = Math.Abs(curr.X - left);
                el.Height = Math.Abs(curr.Y - top);
                Canvas.SetLeft(el, curr.X < left ? curr.X : left);
                Canvas.SetTop(el, curr.Y < top ? curr.Y : top);
            }
        }

        private void RemoveAtPoint(Point p)
        {
            for (int i = drawingCanvas.Children.Count - 1; i >= 0; i--)
            {
                Shape s = drawingCanvas.Children[i] as Shape;
                if (s != null && IsPointNearShape(p, s, brushSize))
                {
                    drawingCanvas.Children.RemoveAt(i); break;
                }
            }
        }

        private bool IsPointNearShape(Point p, Shape s, double t)
        {
            if (s is Line)
            {
                Line l = (Line)s;
                return PointToLineDistance(p, new Point(l.X1, l.Y1), new Point(l.X2, l.Y2)) <= t;
            }
            else if (s is Rectangle)
            {
                Rectangle r = (Rectangle)s;
                return new Rect(Canvas.GetLeft(r), Canvas.GetTop(r), r.Width, r.Height).Contains(p);
            }
            else if (s is Ellipse)
            {
                Ellipse e = (Ellipse)s;
                return new Rect(Canvas.GetLeft(e), Canvas.GetTop(e), e.Width, e.Height).Contains(p);
            }
            else if (s is Polyline)
            {
                Polyline poly = (Polyline)s;
                for (int i = 0; i < poly.Points.Count - 1; i++)
                    if (PointToLineDistance(p, poly.Points[i], poly.Points[i + 1]) <= t) return true;
            }
            return false;
        }

        private double PointToLineDistance(Point p, Point a, Point b)
        {
            double A = p.X - a.X, B = p.Y - a.Y, C = b.X - a.X, D = b.Y - a.Y;
            double dot = A * C + B * D, len_sq = C * C + D * D, param = (len_sq != 0) ? dot / len_sq : -1;
            double xx = param < 0 ? a.X : param > 1 ? b.X : a.X + param * C,
                   yy = param < 0 ? a.Y : param > 1 ? b.Y : a.Y + param * D;
            return Math.Sqrt((p.X - xx) * (p.X - xx) + (p.Y - yy) * (p.Y - yy));
        }

        private void BrushSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            brushSize = brushSizeSlider.Value;
            brushSizeText.Text = brushSize.ToString("0");
        }
        private void ClearButton_Click(object sender, RoutedEventArgs e) 
        {
            undoStack.Push(GetCanvasBitmap());
            drawingCanvas.Children.Clear();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog { Filter = "PNG Image|*.png|JPEG Image|*.jpg|Bitmap Image|*.bmp", Title = "Сохранить рисунок" };
            if (dlg.ShowDialog() == true)
            {
                int w = (int)drawingCanvas.ActualWidth, h = (int)drawingCanvas.ActualHeight;
                drawingCanvas.Background = Brushes.White; // холст гарантированно белый
                RenderTargetBitmap bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(drawingCanvas);
                BitmapEncoder encoder;
                string ext = System.IO.Path.GetExtension(dlg.FileName).ToLower();
                if (ext == ".jpg" || ext == ".jpeg") encoder = new JpegBitmapEncoder();
                else if (ext == ".bmp") encoder = new BmpBitmapEncoder();
                else encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                using (FileStream fs = File.Create(dlg.FileName)) encoder.Save(fs);
                MessageBox.Show("Изображение успешно сохранено!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        void UpdateStatus()
        {
            string name = "Карандаш";
            if (currentTool == Tool.Line) name = "Линия";
            else if (currentTool == Tool.Rectangle) name = "Прямоугольник";
            else if (currentTool == Tool.Ellipse) name = "Эллипс";
            else if (currentTool == Tool.Eraser) name = "Ластик";
            if (statusText != null)
                statusText.Text = "Инструмент: " + name;
        }

        private WriteableBitmap GetCanvasBitmap()
        {
            int w = (int)drawingCanvas.ActualWidth;
            int h = (int)drawingCanvas.ActualHeight;
            var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            drawingCanvas.Background = Brushes.White;
            bmp.Render(drawingCanvas);
            return new WriteableBitmap(bmp);
        }

        private void ApplyFilteredToCanvas(Func<WriteableBitmap, WriteableBitmap> filterFunc, WriteableBitmap srcBitmap)
        {
            int w = srcBitmap.PixelWidth, h = srcBitmap.PixelHeight;
            WriteableBitmap filtered = filterFunc(new WriteableBitmap(srcBitmap));
            drawingCanvas.Children.Clear();
            Image img = new Image() { Source = filtered, Width = w, Height = h };
            Canvas.SetLeft(img, 0);
            Canvas.SetTop(img, 0);
            drawingCanvas.Children.Add(img);
        }

        private void RestoreOriginalCanvasBitmap()
        {
            if (originalCanvasBitmap != null)
            {
                int w = originalCanvasBitmap.PixelWidth, h = originalCanvasBitmap.PixelHeight;
                drawingCanvas.Children.Clear();
                Image img = new Image() { Source = originalCanvasBitmap, Width = w, Height = h };
                Canvas.SetLeft(img, 0); Canvas.SetTop(img, 0);
                drawingCanvas.Children.Add(img);
            }
        }

        private void ToggleFilter_Click(object sender, RoutedEventArgs e)
        {
            if (!filterActive)
            {
                originalCanvasBitmap = GetCanvasBitmap();
                ApplyFilteredToCanvas(BrightnessFilter, originalCanvasBitmap); // или другой фильтр!
                filterActive = true;
            }
            else
            {
                RestoreOriginalCanvasBitmap();
                filterActive = false;
            }
        }

        private WriteableBitmap BrightnessFilter(WriteableBitmap wb)
        {
            int w = wb.PixelWidth, h = wb.PixelHeight, stride = wb.BackBufferStride;
            byte[] pixels = new byte[h * stride];
            wb.CopyPixels(pixels, stride, 0);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * stride + x * 4;
                    pixels[i + 0] = (byte)Math.Min(pixels[i + 0] + 40, 255); // B
                    pixels[i + 1] = (byte)Math.Min(pixels[i + 1] + 40, 255); // G
                    pixels[i + 2] = (byte)Math.Min(pixels[i + 2] + 40, 255); // R
                }
            wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);
            return wb;
        }

        private WriteableBitmap InvertFilter(WriteableBitmap wb)
        {
            int w = wb.PixelWidth, h = wb.PixelHeight, stride = wb.BackBufferStride;
            byte[] pixels = new byte[h * stride];
            wb.CopyPixels(pixels, stride, 0);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * stride + x * 4;
                    pixels[i + 0] = (byte)(255 - pixels[i + 0]);
                    pixels[i + 1] = (byte)(255 - pixels[i + 1]);
                    pixels[i + 2] = (byte)(255 - pixels[i + 2]);
                }
            wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);
            return wb;
        }

        private WriteableBitmap SepiaFilter(WriteableBitmap wb)
        {
            int w = wb.PixelWidth, h = wb.PixelHeight, stride = wb.BackBufferStride;
            byte[] pixels = new byte[h * stride];
            wb.CopyPixels(pixels, stride, 0);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * stride + x * 4;
                    byte b = pixels[i + 0], g = pixels[i + 1], r = pixels[i + 2];
                    byte tr = (byte)Math.Min(0.393 * r + 0.769 * g + 0.189 * b, 255);
                    byte tg = (byte)Math.Min(0.349 * r + 0.686 * g + 0.168 * b, 255);
                    byte tb = (byte)Math.Min(0.272 * r + 0.534 * g + 0.131 * b, 255);
                    pixels[i + 2] = tr; pixels[i + 1] = tg; pixels[i + 0] = tb;
                }
            wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);
            return wb;
        }

        private WriteableBitmap BlurFilter(WriteableBitmap wb)
        {
            int w = wb.PixelWidth, h = wb.PixelHeight, stride = wb.BackBufferStride;
            byte[] src = new byte[h * stride]; wb.CopyPixels(src, stride, 0);
            byte[] dst = new byte[src.Length];
            Array.Copy(src, dst, src.Length);
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    int[] sum = new int[3];
                    for (int ky = -1; ky <= 1; ky++)
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            int ni = ((y + ky) * stride) + (x + kx) * 4;
                            sum[0] += src[ni + 0];
                            sum[1] += src[ni + 1];
                            sum[2] += src[ni + 2];
                        }
                    int idx = y * stride + x * 4;
                    dst[idx + 0] = (byte)(sum[0] / 9); // B
                    dst[idx + 1] = (byte)(sum[1] / 9); // G
                    dst[idx + 2] = (byte)(sum[2] / 9); // R
                }
            wb.WritePixels(new Int32Rect(0, 0, w, h), dst, stride, 0);
            return wb;
        }

        private void ToggleBrightness_Click(object sender, RoutedEventArgs e) { ToggleGenericFilter(BrightnessFilter); }
        private void ToggleInvert_Click(object sender, RoutedEventArgs e) { ToggleGenericFilter(InvertFilter); }
        private void ToggleSepia_Click(object sender, RoutedEventArgs e) { ToggleGenericFilter(SepiaFilter); }
        private void ToggleBlur_Click(object sender, RoutedEventArgs e) { ToggleGenericFilter(BlurFilter); }

        private void ToggleGenericFilter(Func<WriteableBitmap, WriteableBitmap> filter)
        {
            if (!filterActive)
            {
                originalCanvasBitmap = GetCanvasBitmap();
                ApplyFilteredToCanvas(filter, originalCanvasBitmap);
                filterActive = true;
            }
            else
            {
                RestoreOriginalCanvasBitmap();
                filterActive = false;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
            {
                Undo();
                e.Handled = true;
            }
            // Аналогично добавляйте другие комбинации
        }

        private Stack<WriteableBitmap> undoStack = new Stack<WriteableBitmap>();

        private void Undo()
        {
            if (undoStack.Count > 0)
            {
                WriteableBitmap previous = undoStack.Pop();
                int w = previous.PixelWidth, h = previous.PixelHeight;
                drawingCanvas.Children.Clear();
                var img = new Image { Source = previous, Width = w, Height = h };
                Canvas.SetLeft(img, 0); Canvas.SetTop(img, 0);
                drawingCanvas.Children.Add(img);
            }
        }

    }
}
