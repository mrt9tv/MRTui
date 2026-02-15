// Global using directives to resolve ambiguity between
// System.Drawing (pulled in by Velopack) and System.Windows.Media (WPF).
// This WPF app exclusively uses WPF types.

global using Color = System.Windows.Media.Color;
global using Brush = System.Windows.Media.Brush;
global using Brushes = System.Windows.Media.Brushes;
global using Point = System.Windows.Point;
global using Size = System.Windows.Size;
global using Button = System.Windows.Controls.Button;
global using UserControl = System.Windows.Controls.UserControl;
global using Application = System.Windows.Application;
