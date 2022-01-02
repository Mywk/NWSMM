/* Copyright (C) 2021 - Mywk.Net
 * Licensed under the EUPL, Version 1.2
 * You may obtain a copy of the Licence at: https://joinup.ec.europa.eu/community/eupl/og_page/eupl
 * Unless required by applicable law or agreed to in writing, software distributed under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 */
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using NWSMM.Properties;
using Color = System.Drawing.Color;

namespace NWSMM
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly BackgroundWorker _mapWorker = new BackgroundWorker();
        private readonly PositionTracker _positionTracker = new PositionTracker();
        private readonly OCR _ocr = new OCR();

        // Debug mode is used to help developers find out potential problems with the processed images
        private bool _isDebugMode = false;

        // We always keep track of our last position and rotation angle
        private Vector2 _lastPosition = default;
        private double _rotationAngle;

        private bool _windowLoaded = false;

        private bool _webViewReady = false;
        private bool _webViewReRendering = false;

        // Turn worker on by default
        private bool _workerStartPending = true;
        private bool _workerRunning = false;

        /// <summary>
        /// MainWindow constructor
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            _mapWorker.WorkerSupportsCancellation = true;
            _mapWorker.DoWork += mapWorker_DoWork;

            WebView.DefaultBackgroundColor = Color.Transparent;

            LoadSettings();

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            InfoLabel.Content = this.Title + " v" + version.Major + "." + version.Minor + " © " + DateTime.Now.Year + " - Mywk.Net";
        }

        /// <summary>
        /// OnLoaded event, starts worker if necessary and sets the WebView2 cache folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_workerStartPending)
            {
                _workerRunning = true;
                _mapWorker.RunWorkerAsync();
            }

            var coreWebView2Environment = await CoreWebView2Environment.CreateAsync(null, "cache");
            await WebView.EnsureCoreWebView2Async(coreWebView2Environment);

            WebView.CoreWebView2.Navigate("https://mapgenie.io/new-world/maps/aeternum?x=-0.9&y=0.9&zoom=13.5");

            _windowLoaded = true;
        }

        /// <summary>
        /// Adds text to the log window
        /// </summary>
        /// <param name="text"></param>
        private void AddToLog(string text)
        {
            LogTextBox.Text = text + "\r\n" + LogTextBox.Text;
        }

        // Linear interpolation
        private static double Lerp(double x, double y, double by)
        {
            return x * (1 - by) + y * by;
        }

        private static double InvlerpNoClamp(double e, double t, double n)
        {
            return (n - e) / (t - e);
        }

        private static double Range(double e, double n, double o, double r, double i)
        {
            return Lerp(o, r, InvlerpNoClamp(e, n, i));
        }


        /// <summary>
        /// QND calculation for converting from game coordinates to the maps latitude and longitude
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        private double[] ConvertGameCoordinatesToLatLng(double x, double y)
        {
            const int inGameBoundsLeft = 4812;
            const int inGameBoundsRight = 13952;
            const int inGameBoundsTop = 7944;
            const int inGameBoundsBottom = 4532;

            const double mapBoundsTopLeftX = 127.23117148476692;
            const double mapBoundsTopLeftY = 127.33160664985246;

            const double mapBoundsBottomRightX = 127.7893259452204;
            const double mapBoundsBottomRightY = 127.53969981310864;

            var mapX = Range(inGameBoundsLeft, inGameBoundsRight, mapBoundsTopLeftX, mapBoundsBottomRightX, x);
            var mapY = Range(inGameBoundsTop, inGameBoundsBottom, mapBoundsTopLeftY, mapBoundsBottomRightY, y);


            // Now we convert this to the lat/lng
            int n = 256;

            int r = 6371000;
            double e = 0.5 / (Math.PI * r);

            // Un-transform
            var untransformedX = (mapX / n - 0.5) / e;
            var untransformedY = (mapY / n - 0.5) / -e;

            // Un-project
            var t = 180 / Math.PI;


            var lng = untransformedX * t / r;
            var lat = (2 * Math.Atan(Math.Exp(untransformedY / r)) - Math.PI / 2) * t;

            return new double[] { lat, lng };
        }


        /// <summary>
        /// Update the map from the current game position
        /// </summary>
        /// <returns>bool - true if got a valid position</returns>
        private bool Update()
        {
            if (!_webViewReady)
                return false;

            var original = ScreenshotUtil.GetScreenshot(ScreenshotUtil.ScreenshotType.Position);

            if (original == null)
                return false;

            bool isValid = false;

            // Bitmap for processing
            Bitmap bmp = original.Clone(new Rectangle(0, 0, original.Width, original.Height), original.PixelFormat);

            Vector2 validPosition = default;

            // We attempt this three times, first with the original bitmap
            validPosition = _positionTracker.PositionFromText(_ocr.ProcessImageToText(bmp));

            if (validPosition == default)
            {
                // Second attempt
                _ocr.BitmapPreProcessingByThreshold(bmp, Color.FromArgb(255, 253, 228), 15000);
                validPosition = _positionTracker.PositionFromText(_ocr.ProcessImageToText(bmp));

                if (validPosition == default)
                {
                    // Third attempt
                    bmp = original.Clone(new Rectangle(0, 0, original.Width, original.Height), original.PixelFormat);
                    _ocr.BitmapPreProcessingByColorMatch(bmp, 160, 140, 100, 255, 255, 255);
                    validPosition = _positionTracker.PositionFromText(_ocr.ProcessImageToText(bmp));
                }
            }

            if (validPosition != default && validPosition != _lastPosition)
                isValid = _positionTracker.UpdatePosition(validPosition);

            // Update _mapImageCache if the position is correct
            if (isValid)
            {
                var posData = ConvertGameCoordinatesToLatLng(validPosition.X, validPosition.Y);

                Vector2 posDifference = validPosition - _lastPosition;
                if (posDifference != Vector2.Zero)
                {
                    _rotationAngle = Math.Atan2(posDifference.X, posDifference.Y);
                }
                _lastPosition = validPosition;

                Dispatcher.Invoke((Action)async delegate
                {
                    // Update player position
                    var panToString = "window.mapManager.panToLatLng(" + posData[0] + "," +
                                     posData[1] + ");";
                    await WebView.CoreWebView2.ExecuteScriptAsync(panToString);

                    // Update player rotation
                    await WebView.CoreWebView2.ExecuteScriptAsync("document.getElementById('playerPosArrow').style.transform = 'rotate(" + (int)(_rotationAngle * 80) + "deg)'; ");
                });

            }

            // Display image if debug is active
            if (_isDebugMode)
            {
                Dispatcher.Invoke((Action)delegate
               {
                   if (CapturePreview.Source != null)
                       CapturePreview.Source = null;

                   if (isValid)
                   {
                       CapturePreview.Source = _ocr.BitmapToBitmapImage(bmp);
                       AddToLog(validPosition.ToString());
                   }
                   else
                       CapturePreview.Source = null;
               });
            }

            return isValid;
        }

        /// <summary>
        /// Our map worker
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void mapWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                if (_mapWorker.CancellationPending)
                {
                    _workerRunning = false;
                    e.Cancel = true;
                    break;
                }
                else
                {
                    Thread.Sleep(Update() ? 300 : 200);
                }
            }
        }

        /// <summary>
        /// Toggle worker on/off
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnOffToggleButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_mapWorker.IsBusy != true)
            {
                OnOffToggleButton.Content = "⬤";
                _workerRunning = true;
                _mapWorker.RunWorkerAsync();
                Settings.Default.IsOn = true;
            }
            else
            {
                OnOffToggleButton.Content = "◯";
                _mapWorker.CancelAsync();
                Settings.Default.IsOn = false;
            }

            Settings.Default.Save();
        }

        /// <summary>
        /// Closes the app
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_workerRunning)
                _mapWorker.CancelAsync();

            this.Close();
        }

        /// <summary>
        /// Drag move from any UI element (except the WebView2)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TopUIElement_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private async void WebView_OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess && sender != null && sender.GetType() == typeof(WebView2))
            {
                var wbv = ((WebView2)sender);

                // Hide scrolling bars
                await wbv.ExecuteScriptAsync("document.querySelector('body').style.overflow='scroll';var style=document.createElement('style');style.type='text/css';style.innerHTML='::-webkit-scrollbar{display:none}';document.getElementsByTagName('body')[0].appendChild(style)");

                // Hide navigation header and footer
                await wbv.ExecuteScriptAsync("document.getElementsByClassName('ncmp__banner')[0].style.visibility = 'hidden';");
                await wbv.ExecuteScriptAsync("document.getElementsByClassName('social')[0].style.visibility = 'hidden';");
                await wbv.ExecuteScriptAsync("document.getElementsByClassName('tools-panel')[0].style.visibility = 'hidden';");
                await wbv.ExecuteScriptAsync("document.getElementsByClassName('mapboxgl-ctrl-top-right')[0].style.visibility = 'hidden';");
                await wbv.ExecuteScriptAsync("document.getElementById('right-sidebar').style.visibility = 'hidden'; ");
                await wbv.ExecuteScriptAsync("document.getElementById('blobby-left').style.visibility = 'hidden'; ");
                await wbv.ExecuteScriptAsync("document.getElementById('header').style.visibility = 'hidden'; ");

                // Hide the measuring tool and the + and -
                await wbv.ExecuteScriptAsync("document.getElementById('distance-tool-control').style.visibility = 'hidden'; ");
                await wbv.ExecuteScriptAsync("document.querySelectorAll('.mapboxgl-ctrl-group').forEach((el) => {el.style.visibility = 'hidden';});");

                await wbv.ExecuteScriptAsync("document.getElementById('add-note-control').style.visibility = 'hidden'; ");

                // Make map transparent by default
                await wbv.ExecuteScriptAsync("document.getElementById('app').style.opacity = 0.8; ");
                await wbv.ExecuteScriptAsync("document.body.style.background = 'transparent'; ");

                // Finally, let's add something to the middle of the page to act as the middle point
                await wbv.ExecuteScriptAsync("document.getElementById(\"app\").insertAdjacentHTML('afterend', \"<div id='playerPosArrow' style='position: fixed;  top: 50%;  left: 50%;   transform: translate(-50%, -50%);color: white; font-size: 20px; margin: -10px; '>⮝</div>\");");

                await Task.Delay(1000);

                // We may also close the ad video - commented by default, the website author deserves to get their ads shown
                //await Task.Delay(1000);
                //await wbv.ExecuteScriptAsync("document.getElementsByClassName('lre-cancel-float')[0].click(); ");

                LoadingLabel.Visibility = Visibility.Hidden;
                WebView.Visibility = Visibility.Visible;

                _webViewReRendering = true;
                this.Top += 1;
                _webViewReRendering = false;

                await ChangeOpacityAsync(Settings.Default.Opacity);

                // Click on "show all" markers
                await wbv.ExecuteScriptAsync("document.getElementById('show-all').click(); ");

                // And click on the Animals marker to disable them by default, then do the same for quests
                string[] toDisable = { "Animals", "Quests" };
                foreach (var disable in toDisable)
                {
                    await wbv.ExecuteScriptAsync("var aTags = document.getElementsByTagName(\"div\"); for (var i = 0; i < aTags.length; i++) {if (aTags[i].textContent == \"" + disable + "\") { animalsDivFound = aTags[i]; break;  }} animalsDivFound.click(); ");
                }

                _webViewReady = true;
            }
        }

        private bool _uiVisible = true;
        /// <summary>
        /// Toggle UI visibility
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HideToggleButton_OnClick(object sender, RoutedEventArgs e)
        {
            _uiVisible = !_uiVisible;

            if (_uiVisible)
            {
                HideToggleButton.Content = "▼";
                UiGrid.Visibility = Visibility.Visible;
                MainBorder.Background = MainGrid.Background = new SolidColorBrush(Colors.Black);
                ResizeGripCanvas.Opacity = 1;
                WebViewGrid.Opacity = 1;
            }
            else
            {
                HideToggleButton.Content = "▲";
                UiGrid.Visibility = Visibility.Hidden;
                MainBorder.Background = MainGrid.Background = new SolidColorBrush(Colors.Transparent);
                ResizeGripCanvas.Opacity = 0.1;
                WebViewGrid.Opacity = 0;
            }

            Settings.Default.UiActive = _uiVisible;
            Settings.Default.Save();
        }

        /// <summary>
        /// Updates this Window opacity, including the WebView
        /// </summary>
        /// <param name="opacity"></param>
        /// <returns></returns>
        private async Task ChangeOpacityAsync(int opacity)
        {
            this.Opacity = (double)opacity / 100;
            await WebView.ExecuteScriptAsync("document.getElementById('app').style.opacity = " + Opacity + "; ");
        }

        private async void OpacitySlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_webViewReady && !_windowLoaded && !_webViewReRendering) return;

            var opacity = (int)OpacitySlider.Value;

            Settings.Default.Opacity = opacity;
            Settings.Default.Save();

            await ChangeOpacityAsync(opacity);
        }

        private bool _isBigSize = false;
        private void ResizeButton_OnClick(object sender, RoutedEventArgs e)
        {
            _isBigSize = !_isBigSize;

            _windowLoaded = false;

            // Resize window to be big enough to display the left panel
            if (_isBigSize)
            {
                this.Height = 735;
                this.Width = 785;

                ResizeButton.Content = "🗛";
            }
            // Restore last saved size and position
            else
            {
                LoadSettingsSizeAndPosition();
                ResizeButton.Content = "🗚";
            }

            _windowLoaded = true;
        }

        private int _debugCounter = 0;
        /// <summary>
        /// Clicking 3x on the move label opens the debug dialog
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MoveLabel_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount > 0)
            {
                _debugCounter++;

                if (_debugCounter >= 2)
                {
                    _isDebugMode = !_isDebugMode;

                    DebugGrid.Visibility = _isDebugMode ? Visibility.Visible : Visibility.Collapsed;

                    _debugCounter = 0;
                }
            }
        }

        #region Settings

        /// <summary>
        /// Save last known window position
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindow_OnLocationChanged(object? sender, EventArgs e)
        {
            if (!_windowLoaded || _webViewReRendering || _isBigSize) return;

            Settings.Default.WindowLeft = System.Windows.Application.Current.MainWindow.Left;
            Settings.Default.WindowTop = System.Windows.Application.Current.MainWindow.Top;
            Settings.Default.Save();
        }

        /// <summary>
        /// Save last known window size
        /// </summary>
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_windowLoaded || _webViewReRendering || _isBigSize) return;

            Settings.Default.WindowSize = e.NewSize;
            Settings.Default.Save();
        }

        /// <summary>
        /// Loads and applies last saved window size and position
        /// </summary>
        private void LoadSettingsSizeAndPosition()
        {
            // Window size and position
            {
                if (Settings.Default.WindowSize.Width != 0)
                {
                    this.Width = Settings.Default.WindowSize.Width;
                    this.Height = Settings.Default.WindowSize.Height;
                }

                if (Settings.Default.WindowLeft != 0)
                    this.Left = Settings.Default.WindowLeft;

                if (Settings.Default.WindowTop != 0)
                    this.Top = Settings.Default.WindowTop;
            }
        }

        /// <summary>
        /// Load last saved settings if any
        /// </summary>
        private void LoadSettings()
        {
            if (_windowLoaded)
                return;

            try
            {
                // Window size and position
                LoadSettingsSizeAndPosition();

                // Is it on by default?
                if (!Settings.Default.IsOn)
                {
                    _workerStartPending = false;
                    OnOffToggleButton.Content = "◯";
                }

                // UI Toggled?
                if (!Settings.Default.UiActive)
                    HideToggleButton_OnClick(null, null);

                // Update opacity
                OpacitySlider.Value = Settings.Default.Opacity;
            }
            catch (Exception) { }
        }

        #endregion

        private void InfoLabel_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var targetURL = "https://mywk.net/software/newworld-standalone-minimap";
            var psi = new ProcessStartInfo
            {
                FileName = targetURL,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
    }
}
