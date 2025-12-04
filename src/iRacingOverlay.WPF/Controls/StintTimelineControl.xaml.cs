using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using iRacingOverlay.Core.Models;

namespace iRacingOverlay.WPF.Controls
{
    /// <summary>
    /// Phase 10.2: Multi-stint timeline visualization
    /// Shows fuel/tire consumption over race distance with pit stop projections
    /// </summary>
    public partial class StintTimelineControl : UserControl
    {
        private double _zoomLevel = 1.0;
        private const double MIN_ZOOM = 0.5;
        private const double MAX_ZOOM = 3.0;
        private const double ZOOM_STEP = 0.25;
        
        private int _totalLaps = 50;
        private int _currentLap = 1;
        private List<StintProjection> _stints = new();
        
        // Visual settings
        private const double PIXELS_PER_LAP_BASE = 12.0; // Base pixels per lap at 100% zoom
        private const double LAP_MARKER_INTERVAL = 5;    // Show lap number every 5 laps
        
        public StintTimelineControl()
        {
            InitializeComponent();
            UpdateZoomDisplay();
            
            // Wait for layout to complete before drawing
            Loaded += (s, e) => RedrawTimeline();
        }
        
        /// <summary>
        /// Update timeline with race data
        /// </summary>
        public void UpdateTimeline(FuelData fuelData, int totalLaps, List<StintProjection> stints)
        {
            _totalLaps = totalLaps > 0 ? totalLaps : 50;
            _currentLap = fuelData.CurrentLap;
            _stints = stints ?? new List<StintProjection>();
            
            RedrawTimeline();
        }
        
        /// <summary>
        /// Redraw entire timeline
        /// </summary>
        private void RedrawTimeline()
        {
            ClearCanvases();
            
            double pixelsPerLap = PIXELS_PER_LAP_BASE * _zoomLevel;
            double totalWidth = _totalLaps * pixelsPerLap;
            
            // Update canvas widths
            LapMarkersCanvas.Width = totalWidth;
            FuelTimelineCanvas.Width = totalWidth;
            TireTimelineCanvas.Width = totalWidth;
            PitStopCanvas.Width = totalWidth;
            LapScaleCanvas.Width = totalWidth;
            
            // Draw components
            DrawLapMarkers(pixelsPerLap);
            DrawCurrentLapIndicator(pixelsPerLap);
            DrawFuelProjection(pixelsPerLap);
            DrawTireProjection(pixelsPerLap);
            DrawPitStops(pixelsPerLap);
            DrawLapScale(pixelsPerLap);
        }
        
        /// <summary>
        /// Clear all canvases
        /// </summary>
        private void ClearCanvases()
        {
            LapMarkersCanvas.Children.Clear();
            FuelTimelineCanvas.Children.Clear();
            TireTimelineCanvas.Children.Clear();
            PitStopCanvas.Children.Clear();
            LapScaleCanvas.Children.Clear();
        }
        
        /// <summary>
        /// Draw lap marker lines
        /// </summary>
        private void DrawLapMarkers(double pixelsPerLap)
        {
            double canvasHeight = LapMarkersCanvas.ActualHeight > 0 ? LapMarkersCanvas.ActualHeight : 30;
            
            for (int lap = 0; lap <= _totalLaps; lap += (int)LAP_MARKER_INTERVAL)
            {
                double x = lap * pixelsPerLap;
                
                // Vertical line
                var line = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = canvasHeight,
                    Stroke = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                    StrokeThickness = 1
                };
                LapMarkersCanvas.Children.Add(line);
                
                // Lap number
                var text = new TextBlock
                {
                    Text = $"L{lap}",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88))
                };
                Canvas.SetLeft(text, x + 2);
                Canvas.SetTop(text, 5);
                LapMarkersCanvas.Children.Add(text);
            }
        }
        
        /// <summary>
        /// Draw current lap indicator
        /// </summary>
        private void DrawCurrentLapIndicator(double pixelsPerLap)
        {
            double x = _currentLap * pixelsPerLap;
            
            // Current lap line in fuel canvas (spans canvas height)
            var line = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = FuelTimelineCanvas.ActualHeight > 0 ? FuelTimelineCanvas.ActualHeight : 40,
                Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0x80, 0x80)), // Teal
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 }
            };
            FuelTimelineCanvas.Children.Add(line);
            
            // Current lap line in tire canvas
            var tireLine = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = TireTimelineCanvas.ActualHeight > 0 ? TireTimelineCanvas.ActualHeight : 40,
                Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0x80, 0x80)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 }
            };
            TireTimelineCanvas.Children.Add(tireLine);
        }
        
        /// <summary>
        /// Draw fuel consumption projection
        /// </summary>
        private void DrawFuelProjection(double pixelsPerLap)
        {
            if (_stints.Count == 0)
            {
                DrawPlaceholderText(FuelTimelineCanvas, "Calculating fuel projection...");
                return;
            }
            
            double canvasHeight = FuelTimelineCanvas.ActualHeight > 0 ? FuelTimelineCanvas.ActualHeight : 40;
            var points = new PointCollection();
            
            foreach (var stint in _stints)
            {
                // Start of stint
                double startX = stint.StartLap * pixelsPerLap;
                double startY = canvasHeight * (1 - stint.StartFuelPercent / 100.0);
                points.Add(new Point(startX, startY));
                
                // End of stint
                double endX = stint.EndLap * pixelsPerLap;
                double endY = canvasHeight * (1 - stint.EndFuelPercent / 100.0);
                points.Add(new Point(endX, endY));
            }
            
            // Draw polyline
            var polyline = new Polyline
            {
                Points = points,
                Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x80, 0x00)), // Orange
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round
            };
            FuelTimelineCanvas.Children.Add(polyline);
            
            // Fill area under curve
            var fillPoints = new PointCollection(points);
            fillPoints.Add(new Point(points[points.Count - 1].X, canvasHeight));
            fillPoints.Add(new Point(points[0].X, canvasHeight));
            
            var polygon = new Polygon
            {
                Points = fillPoints,
                Fill = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0x80, 0x00)),
                Stroke = null
            };
            FuelTimelineCanvas.Children.Insert(0, polygon); // Add behind polyline
        }
        
        /// <summary>
        /// Draw tire wear projection
        /// </summary>
        private void DrawTireProjection(double pixelsPerLap)
        {
            if (_stints.Count == 0)
            {
                DrawPlaceholderText(TireTimelineCanvas, "Calculating tire wear...");
                return;
            }
            
            double canvasHeight = TireTimelineCanvas.ActualHeight > 0 ? TireTimelineCanvas.ActualHeight : 40;
            var points = new PointCollection();
            
            foreach (var stint in _stints)
            {
                // Start of stint
                double startX = stint.StartLap * pixelsPerLap;
                double startY = canvasHeight * (1 - stint.StartTirePercent / 100.0);
                points.Add(new Point(startX, startY));
                
                // End of stint
                double endX = stint.EndLap * pixelsPerLap;
                double endY = canvasHeight * (1 - stint.EndTirePercent / 100.0);
                points.Add(new Point(endX, endY));
            }
            
            // Draw polyline
            var polyline = new Polyline
            {
                Points = points,
                Stroke = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)), // Green
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round
            };
            TireTimelineCanvas.Children.Add(polyline);
            
            // Fill area under curve
            var fillPoints = new PointCollection(points);
            fillPoints.Add(new Point(points[points.Count - 1].X, canvasHeight));
            fillPoints.Add(new Point(points[0].X, canvasHeight));
            
            var polygon = new Polygon
            {
                Points = fillPoints,
                Fill = new SolidColorBrush(Color.FromArgb(0x30, 0x4C, 0xAF, 0x50)),
                Stroke = null
            };
            TireTimelineCanvas.Children.Insert(0, polygon);
        }
        
        /// <summary>
        /// Draw pit stop markers
        /// </summary>
        private void DrawPitStops(double pixelsPerLap)
        {
            double canvasHeight = PitStopCanvas.ActualHeight > 0 ? PitStopCanvas.ActualHeight : 30;
            
            foreach (var stint in _stints)
            {
                if (stint.EndLap < _totalLaps) // Don't show pit after final lap
                {
                    double x = stint.EndLap * pixelsPerLap;
                    
                    // Pit stop icon (vertical bar with "PIT" label)
                    var rect = new Rectangle
                    {
                        Width = 4,
                        Height = canvasHeight,
                        Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x33, 0x33)) // Red
                    };
                    Canvas.SetLeft(rect, x - 2);
                    Canvas.SetTop(rect, 0);
                    PitStopCanvas.Children.Add(rect);
                    
                    // "PIT" label
                    var text = new TextBlock
                    {
                        Text = "PIT",
                        FontSize = 9,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x33, 0x33)),
                        Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x1A, 0x1A, 0x1A))
                    };
                    Canvas.SetLeft(text, x + 5);
                    Canvas.SetTop(text, 10);
                    PitStopCanvas.Children.Add(text);
                }
            }
        }
        
        /// <summary>
        /// Draw lap scale at bottom
        /// </summary>
        private void DrawLapScale(double pixelsPerLap)
        {
            int interval = _zoomLevel < 0.75 ? 10 : 5; // Adjust interval based on zoom
            
            for (int lap = 0; lap <= _totalLaps; lap += interval)
            {
                double x = lap * pixelsPerLap;
                
                // Tick mark
                var line = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = 8,
                    Stroke = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                    StrokeThickness = 1
                };
                LapScaleCanvas.Children.Add(line);
                
                // Lap number
                var text = new TextBlock
                {
                    Text = lap.ToString(),
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88))
                };
                Canvas.SetLeft(text, x - 8);
                Canvas.SetTop(text, 10);
                LapScaleCanvas.Children.Add(text);
            }
        }
        
        /// <summary>
        /// Draw placeholder text in canvas
        /// </summary>
        private void DrawPlaceholderText(Canvas canvas, string message)
        {
            var text = new TextBlock
            {
                Text = message,
                FontSize = 11,
                FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66))
            };
            double canvasHeight = canvas.ActualHeight > 0 ? canvas.ActualHeight : 40;
            Canvas.SetLeft(text, 10);
            Canvas.SetTop(text, canvasHeight / 2 - 10);
            canvas.Children.Add(text);
        }
        
        /// <summary>
        /// Handle zoom in
        /// </summary>
        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (_zoomLevel < MAX_ZOOM)
            {
                _zoomLevel += ZOOM_STEP;
                UpdateZoomDisplay();
                RedrawTimeline();
            }
        }
        
        /// <summary>
        /// Handle zoom out
        /// </summary>
        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (_zoomLevel > MIN_ZOOM)
            {
                _zoomLevel -= ZOOM_STEP;
                UpdateZoomDisplay();
                RedrawTimeline();
            }
        }
        
        /// <summary>
        /// Update zoom level display
        /// </summary>
        private void UpdateZoomDisplay()
        {
            ZoomLevelText.Text = $"Zoom: {_zoomLevel * 100:F0}%";
        }
    }
    
    /// <summary>
    /// Represents a single stint in the race
    /// </summary>
    public class StintProjection
    {
        public int StintNumber { get; set; }
        public int StartLap { get; set; }
        public int EndLap { get; set; }
        public double StartFuelPercent { get; set; }
        public double EndFuelPercent { get; set; }
        public double StartTirePercent { get; set; }
        public double EndTirePercent { get; set; }
        public bool IsPitStopPlanned { get; set; }
    }
}
