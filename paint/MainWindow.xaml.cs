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

namespace SimplePaint
{
    public partial class MainWindow : Window
    {
        private enum DrawingTool
        {
            Pencil,
            Line,
            Rectangle,
            Ellipse,
            Eraser
        }

        private DrawingTool currentTool = DrawingTool.Pencil;
        private Point startPoint;
        private Shape currentShape;
        private Polyline currentFreehandLine;
        private List<Point> freehandPoints = new List<Point>();
        private bool isDrawing = false;
        private Color currentColor = Colors.Black;
        private double brushSize = 5;

        public MainWindow()
        {
            InitializeComponent();
            brushSizeSlider.ValueChanged += BrushSizeSlider_ValueChanged;

            InitializeColorPalette();
            currentColor = Colors.Black;
            UpdateStatus();
        }

        private void InitializeColorPalette()
        {
            Color[] colors = {
                Colors.Black, Colors.White, Colors.Red, Colors.Green, Colors.Blue, Colors.Yellow
            };

            colorPalette.ItemsSource = colors;
        }

        private void ColorRectangle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Rectangle rectangle && rectangle.Fill is SolidColorBrush brush)
            {
                currentColor = brush.Color;
            }
        }

        private void ToolComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (toolComboBox.SelectedIndex >= 0)
            {
                currentTool = (DrawingTool)toolComboBox.SelectedIndex;
                UpdateStatus();
            }
        }

        private void BrushSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            brushSize = brushSizeSlider.Value;
            brushSizeText.Text = brushSize.ToString("0");
        }

        private void DrawingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (drawingCanvas == null) return;

            startPoint = e.GetPosition(drawingCanvas);
            isDrawing = true;

            switch (currentTool)
            {
                case DrawingTool.Pencil:
                    currentFreehandLine = new Polyline
                    {
                        Stroke = new SolidColorBrush(currentColor),
                        StrokeThickness = brushSize,
                        StrokeLineJoin = PenLineJoin.Round,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round
                    };
                    freehandPoints.Clear();
                    freehandPoints.Add(startPoint);
                    currentFreehandLine.Points = new PointCollection(freehandPoints);
                    drawingCanvas.Children.Add(currentFreehandLine);
                    break;

                case DrawingTool.Eraser:
                    RemoveElementAtPoint(startPoint);
                    break;

                case DrawingTool.Line:
                case DrawingTool.Rectangle:
                case DrawingTool.Ellipse:
                    CreateShape();
                    break;
            }
        }

        private void DrawingCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (drawingCanvas == null) return;

            var currentPoint = e.GetPosition(drawingCanvas);
            if (coordinatesText != null)
            {
                coordinatesText.Text = $"X: {(int)currentPoint.X}, Y: {(int)currentPoint.Y}";
            }

            if (!isDrawing) return;

            switch (currentTool)
            {
                case DrawingTool.Pencil:
                    if (currentFreehandLine != null)
                    {
                        freehandPoints.Add(currentPoint);
                        currentFreehandLine.Points = new PointCollection(freehandPoints);
                    }
                    break;

                case DrawingTool.Eraser:
                    RemoveElementAtPoint(currentPoint);
                    break;

                case DrawingTool.Line:
                case DrawingTool.Rectangle:
                case DrawingTool.Ellipse:
                    UpdateShape(currentPoint);
                    break;
            }
        }

        private void DrawingCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isDrawing) return;

            isDrawing = false;

            switch (currentTool)
            {
                case DrawingTool.Pencil:
                    currentFreehandLine = null;
                    freehandPoints.Clear();
                    break;

                case DrawingTool.Line:
                case DrawingTool.Rectangle:
                case DrawingTool.Ellipse:
                    currentShape = null;
                    break;
            }
        }

        private void CreateShape()
        {
            if (drawingCanvas == null) return;

            switch (currentTool)
            {
                case DrawingTool.Line:
                    currentShape = new Line
                    {
                        Stroke = new SolidColorBrush(currentColor),
                        StrokeThickness = brushSize,
                        X1 = startPoint.X,
                        Y1 = startPoint.Y,
                        X2 = startPoint.X,
                        Y2 = startPoint.Y,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round
                    };
                    drawingCanvas.Children.Add(currentShape);
                    break;

                case DrawingTool.Rectangle:
                    currentShape = new Rectangle
                    {
                        Stroke = new SolidColorBrush(currentColor),
                        StrokeThickness = brushSize,
                        Fill = Brushes.Transparent
                    };
                    Canvas.SetLeft(currentShape, startPoint.X);
                    Canvas.SetTop(currentShape, startPoint.Y);
                    drawingCanvas.Children.Add(currentShape);
                    break;

                case DrawingTool.Ellipse:
                    currentShape = new Ellipse
                    {
                        Stroke = new SolidColorBrush(currentColor),
                        StrokeThickness = brushSize,
                        Fill = Brushes.Transparent
                    };
                    Canvas.SetLeft(currentShape, startPoint.X);
                    Canvas.SetTop(currentShape, startPoint.Y);
                    drawingCanvas.Children.Add(currentShape);
                    break;
            }
        }

        private void UpdateShape(Point currentPoint)
        {
            if (currentShape == null) return;

            switch (currentTool)
            {
                case DrawingTool.Line:
                    var line = (Line)currentShape;
                    line.X2 = currentPoint.X;
                    line.Y2 = currentPoint.Y;
                    break;

                case DrawingTool.Rectangle:
                    var rect = (Rectangle)currentShape;
                    double rectWidth = currentPoint.X - startPoint.X;
                    double rectHeight = currentPoint.Y - startPoint.Y;
                    rect.Width = Math.Abs(rectWidth);
                    rect.Height = Math.Abs(rectHeight);
                    Canvas.SetLeft(rect, rectWidth < 0 ? currentPoint.X : startPoint.X);
                    Canvas.SetTop(rect, rectHeight < 0 ? currentPoint.Y : startPoint.Y);
                    break;

                case DrawingTool.Ellipse:
                    var ellipse = (Ellipse)currentShape;
                    double ellipseWidth = currentPoint.X - startPoint.X;
                    double ellipseHeight = currentPoint.Y - startPoint.Y;
                    ellipse.Width = Math.Abs(ellipseWidth);
                    ellipse.Height = Math.Abs(ellipseHeight);
                    Canvas.SetLeft(ellipse, ellipseWidth < 0 ? currentPoint.X : startPoint.X);
                    Canvas.SetTop(ellipse, ellipseHeight < 0 ? currentPoint.Y : startPoint.Y);
                    break;
            }
        }

        private void RemoveElementAtPoint(Point point)
        {
            if (drawingCanvas == null) return;

            for (int i = drawingCanvas.Children.Count - 1; i >= 0; i--)
            {
                var element = drawingCanvas.Children[i];
                if (element is Shape shape)
                {
                    if (IsPointNearShape(point, shape, brushSize))
                    {
                        drawingCanvas.Children.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private bool IsPointNearShape(Point point, Shape shape, double tolerance)
        {
            if (shape is Line line)
            {
                return IsPointNearLine(point, new Point(line.X1, line.Y1), new Point(line.X2, line.Y2), tolerance);
            }
            else if (shape is Rectangle rect)
            {
                var left = Canvas.GetLeft(rect);
                var top = Canvas.GetTop(rect);
                var rectBounds = new Rect(left, top, rect.Width, rect.Height);
                return rectBounds.Contains(point);
            }
            else if (shape is Ellipse ellipse)
            {
                var left = Canvas.GetLeft(ellipse);
                var top = Canvas.GetTop(ellipse);
                var ellipseBounds = new Rect(left, top, ellipse.Width, ellipse.Height);
                return ellipseBounds.Contains(point);
            }
            else if (shape is Polyline polyline)
            {
                for (int i = 0; i < polyline.Points.Count - 1; i++)
                {
                    if (IsPointNearLine(point, polyline.Points[i], polyline.Points[i + 1], tolerance))
                        return true;
                }
            }
            return false;
        }

        private bool IsPointNearLine(Point point, Point lineStart, Point lineEnd, double tolerance)
        {
            double distance = PointToLineDistance(point, lineStart, lineEnd);
            return distance <= tolerance;
        }

        private double PointToLineDistance(Point point, Point lineStart, Point lineEnd)
        {
            double A = point.X - lineStart.X;
            double B = point.Y - lineStart.Y;
            double C = lineEnd.X - lineStart.X;
            double D = lineEnd.Y - lineStart.Y;

            double dot = A * C + B * D;
            double len_sq = C * C + D * D;
            double param = (len_sq != 0) ? dot / len_sq : -1;

            double xx, yy;

            if (param < 0)
            {
                xx = lineStart.X;
                yy = lineStart.Y;
            }
            else if (param > 1)
            {
                xx = lineEnd.X;
                yy = lineEnd.Y;
            }
            else
            {
                xx = lineStart.X + param * C;
                yy = lineStart.Y + param * D;
            }

            double dx = point.X - xx;
            double dy = point.Y - yy;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (drawingCanvas != null)
            {
                drawingCanvas.Children.Clear();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "PNG Image|*.png",
                Title = "Сохранить рисунок"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
                        (int)drawingCanvas.ActualWidth,
                        (int)drawingCanvas.ActualHeight,
                        96d, 96d, PixelFormats.Pbgra32);

                    renderBitmap.Render(drawingCanvas);

                    BitmapEncoder encoder;
                    string extension = System.IO.Path.GetExtension(saveDialog.FileName).ToLower();

                    switch (extension)
                    {
                        case ".jpg":
                        case ".jpeg":
                            encoder = new JpegBitmapEncoder();
                            break;
                        case ".bmp":
                            encoder = new BmpBitmapEncoder();
                            break;
                        default:
                            encoder = new PngBitmapEncoder();
                            break;
                    }

                    encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

                    using (FileStream file = File.Create(saveDialog.FileName))
                    {
                        encoder.Save(file);
                    }

                    MessageBox.Show("Изображение успешно сохранено!", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка при сохранении: " + ex.Message, "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateStatus()
        {
            string toolName;

            switch (currentTool)
            {
                case DrawingTool.Pencil:
                    toolName = "Карандаш";
                    break;
                case DrawingTool.Line:
                    toolName = "Линия";
                    break;
                case DrawingTool.Rectangle:
                    toolName = "Прямоугольник";
                    break;
                case DrawingTool.Ellipse:
                    toolName = "Эллипс";
                    break;
                case DrawingTool.Eraser:
                    toolName = "Ластик";
                    break;
                default:
                    toolName = "Карандаш";
                    break;
            }

            if (statusText != null)
            {
                statusText.Text = "Инструмент: " + toolName;
            }
        }
    }
}