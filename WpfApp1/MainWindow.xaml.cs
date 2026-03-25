using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfApp1.Variables;
using static System.Net.Mime.MediaTypeNames;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private bool _isDragging = false;
        private Rectangle _activeRect = null;
        private Canvas _activeCanvas = null;
        private Border _floatingLabel = null;
        private SerialPort _serialPort = null;
        private List<byte> _receiveBuffer = new List<byte>();
        private bool _isConnected = false;
        const byte header01 = 0x02;
        const byte header02 = 0x03;
        const byte footer01 = 0xAA;
        const byte footer02 = 0x55;

        public MainWindow()
        {
            InitializeComponent();
            AtualizarPortas();
            ComboPorts.SelectedIndex = 0;

            this.Loaded += (s, e) =>
            {
                SetupPedal(CtrlAccel, Brushes.Green);
                SetupPedal(CtrlBrake, Brushes.Red);
                SetupPedal(CtrlClutch, Brushes.Orange);
                SetupPedal(CtrlHand, Brushes.Cyan);
            };
        }

        private void apertouComboBox(object sender, EventArgs e) => AtualizarPortas();

        private void AtualizarPortas()
        {
            try
            {
                string[] ports = SerialPort.GetPortNames();
                ComboPorts.ItemsSource = ports.Length != 0 ? ports : new string[] { "Nenhuma porta encontrada" };

            }
            catch { }
        }



        private void SetupPedal(ContentControl container, Brush color)
        {
            Grid mainGrid = new Grid();
            // Row 0: Titulo | Row 1-5: Gráfico | Row 6: Eixo X
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Titulo
            for (int i = 0; i < 5; i++) mainGrid.RowDefinitions.Add(new RowDefinition());
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Legenda X

            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Legenda Y
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 1. ADICIONANDO O TÍTULO (Faltava aqui!)
            TextBlock txtTitle = new TextBlock
            {
                Text = container.Tag.ToString(),
                Foreground = color,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetRow(txtTitle, 0); Grid.SetColumn(txtTitle, 1);
            mainGrid.Children.Add(txtTitle);

            // 2. ESCALA Y (FORÇA) - Alinhamento corrigido
            string[] yLabels = { "100%", "80%", "60%", "40%", "20%", "0%" };
            for (int i = 0; i < 6; i++)
            {
                var tb = new TextBlock { Text = yLabels[i], FontSize = 10, Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 8, 0) };
                Grid.SetColumn(tb, 0);
                Grid.SetRow(tb, i + 1); // +1 por causa do título

                // Alinhamento vertical para bater com as linhas da grade
                if (i == 0) tb.VerticalAlignment = VerticalAlignment.Top;
                else if (i == 5) tb.VerticalAlignment = VerticalAlignment.Bottom;
                else
                {
                    tb.VerticalAlignment = VerticalAlignment.Top;
                    tb.Margin = new Thickness(0, -7, 8, 0); // Compensação para centralizar na linha
                }
                mainGrid.Children.Add(tb);
            }

            // 3. CANVAS
            Border bdr = new Border { Background = new SolidColorBrush(Color.FromRgb(10, 10, 10)), BorderBrush = Brushes.DimGray, BorderThickness = new Thickness(1) };
            Canvas cvs = new Canvas { Background = Brushes.Transparent, ClipToBounds = true };
            cvs.MouseDown += Canvas_MouseDown;
            cvs.MouseMove += Canvas_MouseMove;
            cvs.MouseUp += Canvas_MouseUp;

            bdr.Child = cvs;
            Grid.SetRow(bdr, 1); Grid.SetRowSpan(bdr, 5); Grid.SetColumn(bdr, 1);
            mainGrid.Children.Add(bdr);

            // 4. ESCALA X (PERCURSO)
            UniformGrid scaleX = new UniformGrid { Columns = 6, Margin = new Thickness(0, 8, 0, 0) };
            for (int i = 0; i <= 5; i++) scaleX.Children.Add(new TextBlock { Text = (i * 20) + "%", FontSize = 10, Foreground = Brushes.Gray });
            Grid.SetRow(scaleX, 6); Grid.SetColumn(scaleX, 1);
            mainGrid.Children.Add(scaleX);

            container.Content = mainGrid;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Desenhar Grade
                for (int i = 0; i <= 5; i++)
                {
                    double x = (cvs.ActualWidth / 5) * i;
                    double y = (cvs.ActualHeight / 5) * i;
                    cvs.Children.Add(new Line { X1 = 0, X2 = cvs.ActualWidth, Y1 = y, Y2 = y, Stroke = new SolidColorBrush(Color.FromRgb(30, 30, 30)) });
                    cvs.Children.Add(new Line { X1 = x, X2 = x, Y1 = 0, Y2 = cvs.ActualHeight, Stroke = new SolidColorBrush(Color.FromRgb(30, 30, 30)) });
                }

                Path path = new Path { Stroke = color, StrokeThickness = 2 };
                cvs.Children.Add(path);

                Border label = new Border { Background = Brushes.Black, BorderBrush = color, BorderThickness = new Thickness(1), Padding = new Thickness(5), CornerRadius = new CornerRadius(3), Visibility = Visibility.Collapsed };
                label.Child = new TextBlock { Foreground = Brushes.White, FontSize = 10, FontWeight = FontWeights.Bold };
                Canvas.SetZIndex(label, 100);
                cvs.Children.Add(label);

                for (int i = 0; i < 7; i++)
                {
                    Rectangle r = new Rectangle { Width = 12, Height = 12, Fill = color, Stroke = Brushes.Black, Tag = i, Cursor = Cursors.Hand };
                    double x = (cvs.ActualWidth / 6) * i;
                    double y = cvs.ActualHeight - ((cvs.ActualHeight / 6) * i);
                    Canvas.SetLeft(r, x - 6); Canvas.SetTop(r, y - 6);
                    cvs.Children.Add(r);
                }
                UpdateCurve(cvs);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Rectangle rect)
            {
                _isDragging = true; _activeRect = rect; _activeCanvas = (Canvas)sender;
                _floatingLabel = _activeCanvas.Children.OfType<Border>().First();
                _floatingLabel.Visibility = Visibility.Visible;
                rect.CaptureMouse();
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || _activeRect == null) return;
            Point p = e.GetPosition(_activeCanvas);
            int idx = (int)_activeRect.Tag;
            var rects = _activeCanvas.Children.OfType<Rectangle>().OrderBy(r => (int)r.Tag).ToList();

            double x = p.X;
            double y = Math.Max(0, Math.Min(_activeCanvas.ActualHeight, p.Y));

            // LÓGICA DE BLOQUEIO (Não ultrapassa vizinhos)
            if (idx == 0) { x = 0; y = _activeCanvas.ActualHeight; }
            else if (idx == 6) { x = _activeCanvas.ActualWidth; y = 0; }
            else
            {
                double minX = Canvas.GetLeft(rects[idx - 1]) + 15;
                double maxX = Canvas.GetLeft(rects[idx + 1]) - 15;
                x = Math.Max(minX, Math.Min(maxX, x));
            }

            Canvas.SetLeft(_activeRect, x - 6);
            Canvas.SetTop(_activeRect, y - 6);
            UpdateCurve(_activeCanvas);

            double percX = (x / _activeCanvas.ActualWidth) * 100;
            double percY = (1 - (y / _activeCanvas.ActualHeight)) * 100;
            ((TextBlock)_floatingLabel.Child).Text = $"Força: {percY:0}% / Percurso: {percX:0}%";
            Canvas.SetLeft(_floatingLabel, x + 15); Canvas.SetTop(_floatingLabel, y - 40);
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            if (_floatingLabel != null) _floatingLabel.Visibility = Visibility.Collapsed;
            _activeRect?.ReleaseMouseCapture();
            _activeRect = null;
        }

        private void UpdateCurve(Canvas cvs)
        {
            var path = cvs.Children.OfType<Path>().FirstOrDefault();
            if (path == null) return;
            var pts = cvs.Children.OfType<Rectangle>().OrderBy(r => (int)r.Tag)
                         .Select(r => new Point(Canvas.GetLeft(r) + 6, Canvas.GetTop(r) + 6)).ToList();

            // Algoritmo Monotônico (Clamped) para evitar overshoot
            int n = pts.Count;
            double[] x = pts.Select(p => p.X).ToArray();
            double[] y = pts.Select(p => p.Y).ToArray();
            double[] ms = new double[n - 1];
            for (int i = 0; i < n - 1; i++) ms[i] = (y[i + 1] - y[i]) / (x[i + 1] - x[i]);

            double[] tangents = new double[n];
            for (int i = 1; i < n - 1; i++)
            {
                if (ms[i - 1] * ms[i] <= 0) tangents[i] = 0;
                else tangents[i] = (ms[i - 1] + ms[i]) / 2.0;
            }
            tangents[0] = ms[0]; tangents[n - 1] = ms[n - 2];

            PathGeometry geo = new PathGeometry();
            PathFigure fig = new PathFigure { StartPoint = pts[0] };
            for (int i = 0; i < n - 1; i++)
            {
                double dx = (x[i + 1] - x[i]) / 3.0;
                Point cp1 = new Point(x[i] + dx, y[i] + dx * tangents[i]);
                Point cp2 = new Point(x[i + 1] - dx, y[i + 1] - dx * tangents[i + 1]);

                // Trava para evitar que a curva saia do limite vertical dos dois pontos
                double minY = Math.Min(y[i], y[i + 1]);
                double maxY = Math.Max(y[i], y[i + 1]);
                cp1.Y = Math.Max(minY, Math.Min(maxY, cp1.Y));
                cp2.Y = Math.Max(minY, Math.Min(maxY, cp2.Y));

                fig.Segments.Add(new BezierSegment(cp1, cp2, pts[i + 1], true));
            }
            geo.Figures.Add(fig);
            path.Data = geo;
        }

        private void ButtonConnect_Click(object sender, RoutedEventArgs e)
        {
            if (_isConnected)
            {
                _serialPort.DataReceived -= SerialPort_DataReceived; // Remove evento
                _serialPort.Close();
                _isConnected = false;
                ButtonConnect.Content = "CONECTAR";
                ButtonConnect.Background = new SolidColorBrush(Color.FromRgb(0, 122, 204));
            }
            else
            {
                string selectedPort = ComboPorts.SelectedItem as string;
                if (selectedPort != null && selectedPort.Contains("COM"))
                {
                    try
                    {
                        _serialPort = new SerialPort(selectedPort, 115200);
                        _serialPort.DataReceived += SerialPort_DataReceived; // Adiciona evento
                        _serialPort.Open();
                        _serialPort.DiscardInBuffer();
                        _serialPort.DiscardOutBuffer();
                        _isConnected = true;
                        ButtonConnect.Content = "DESCONECTAR";
                        ButtonConnect.Background = Brushes.DarkRed;
                        TxtSerialLog.AppendText($"--- Conectado em {selectedPort} ---\n");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Erro ao abrir porta: " + ex.Message);
                    }
                }
            }
        }



        private unsafe void ButtonSendData_Click(object sender, RoutedEventArgs e)
        {
            ushort force = ushort.Parse(TxtForce.Text);
            ushort distance = ushort.Parse(TxtDistance.Text);
            byte abs = 0;
            if (ChkAbs.IsChecked == true) {
                abs = 1;
            }

            MyFirstVariable var = new MyFirstVariable
            {
                header = new Header { startHeader01 = 0x02, startHeader02 = 0x03, type = 0 },
                body = new Body { force = force, distance = distance, abs =  abs},
                footer = new Footer { endFooter01 = 0xAA, endFooter02 = 0x55, checkSum = 0 }
            };

            // 1️⃣ Serializa SEM checksum
            byte[] buffer = getBytes_Action(var);

            // 2️⃣ Calcula checksum
            ushort checksum;
            fixed (byte* p = buffer)
            {
                checksum = checksumCalc(p, buffer.Length - sizeof(ushort));
            }

            // 3️⃣ Atualiza struct
            var.footer.checkSum = checksum;

            // 4️⃣ Serializa DE NOVO (AGORA CORRETO)
            buffer = getBytes_Action(var);

            if (!_serialPort.IsOpen)
            {
                MessageBox.Show("Porta não está aberta!");
                return;
            }

            _serialPort.Write(buffer, 0, buffer.Length);
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!_serialPort.IsOpen) return;

            try
            {
                int bytesToRead = _serialPort.BytesToRead;
                byte[] buffer = new byte[bytesToRead];
                _serialPort.Read(buffer, 0, bytesToRead);

                lock (_receiveBuffer)
                {
                    _receiveBuffer.AddRange(buffer);
                }
                ProcessBuffer();
                
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => TxtSerialLog.AppendText($"\n[ERRO]: {ex.Message}\n"));
            }
        }

        public unsafe void ProcessBuffer()
        {
            lock (_receiveBuffer)
            {
                int bufferIndex = 0;
                int secondVariableSize = Marshal.SizeOf<MySecondVariable>();
                int loadcellValuesSize = Marshal.SizeOf<LoadcellValues>();
                while (bufferIndex < _receiveBuffer.Count)
                {
                    if (bufferIndex + secondVariableSize > _receiveBuffer.Count || _receiveBuffer[bufferIndex] != header01)
                    {
                        int start = bufferIndex;
                        while (bufferIndex < _receiveBuffer.Count)
                        {
                            if (_receiveBuffer[bufferIndex] == header01 && bufferIndex + 2 < _receiveBuffer.Count) break;
                            bufferIndex++;
                        }


                        byte[] textByte = _receiveBuffer.Skip(start).Take(bufferIndex - start).ToArray();
                        string text = System.Text.Encoding.ASCII.GetString(textByte);
                        Dispatcher.Invoke(() =>
                        {
                            TxtSerialLog.AppendText(text);
                            TxtSerialLog.ScrollToEnd();
                        });

                    } else
                    {
                        if (_receiveBuffer[bufferIndex + 1] != header02) 
                        {
                            bufferIndex++;
                            continue;
                        }

                        byte type = _receiveBuffer[bufferIndex + 2];

                        if (type != 1 && type != 2) 
                        {
                            bufferIndex++;
                            continue;
                        }

                       int sizeVariable = type == 1 ? secondVariableSize : loadcellValuesSize;
                       
                        if (bufferIndex + sizeVariable > _receiveBuffer.Count) break; 

                        byte[] packet = _receiveBuffer.Skip(bufferIndex).Take(sizeVariable).ToArray();
                        if (ValidateChecksum(packet))
                        {
                            bufferIndex += sizeVariable;
                            if (type == 1)
                            {
                                var data = ByteArrayToStruct<MySecondVariable>(packet);
                                Dispatcher.Invoke(() =>
                                {
                                    TxtSerialLog.AppendText($"\n[RECEBIDO] {data.body.randomNumber}\n");
                                    TxtSerialLog.ScrollToEnd();
                                });
                            } else
                            {
                                var data = ByteArrayToStruct<LoadcellValues>(packet);
                                Dispatcher.Invoke(() =>
                                {
                                    UpdateIndicator(EllAccel, TxtAccelPerc, data.body.throttleForce);
                                    UpdateIndicator(EllBrake, TxtBrakePerc, data.body.brakeForce);
                                    UpdateIndicator(EllClutch, TxtClutchPerc, data.body.clutchForce);
                                    UpdateIndicator(EllHand, TxtHandPerc, data.body.handbrakeForce);
                                    TxtSerialLog.AppendText($"\n[RECEBIDO] Freio de mão {data.body.handbrakeForce}\n[RECEBIDO] Freio  {data.body.brakeForce}\n[RECEBIDO] Acelerador {data.body.throttleForce}\n[RECEBIDO] Embreagem {data.body.clutchForce}\n");
                                    TxtSerialLog.ScrollToEnd();
                                });
                            }
                        }
                        else
                        {
                            bufferIndex++;
                        }

                    }
                }
                if (bufferIndex > 0)
                {
                    _receiveBuffer.RemoveRange(0, bufferIndex);
                }
            } 
        }

        public T ByteArrayToStruct<T>(byte[] data) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }

        private bool ValidateChecksum(byte[] data)
        {
            // últimos 2 bytes = checksum recebido
            ushort receivedChecksum = BitConverter.ToUInt16(data, data.Length - 2);

            unsafe
            {
                fixed (byte* ptr = data)
                {
                    ushort calculatedChecksum = checksumCalc(ptr, data.Length - 2);
                    return receivedChecksum == calculatedChecksum;
                }
            }
        }
        public byte[] getBytes_Action(MyFirstVariable aux)
        {
            int length = Marshal.SizeOf(aux);
            IntPtr ptr = Marshal.AllocHGlobal(length);
            byte[] myBuffer = new byte[length];

            Marshal.StructureToPtr(aux, ptr, true);
            Marshal.Copy(ptr, myBuffer, 0, length);
            Marshal.FreeHGlobal(ptr);

            return myBuffer;
        }



        unsafe public UInt16 checksumCalc(byte* data, int length)
        {

            UInt16 curr_crc = 0x0000;
            byte sum1 = (byte)curr_crc;
            byte sum2 = (byte)(curr_crc >> 8);
            int index;
            for (index = 0; index < length; index = index + 1)
            {
                int v = (sum1 + (*data));
                sum1 = (byte)v;
                sum1 = (byte)(v % 255);

                int w = (sum1 + sum2) % 255;
                sum2 = (byte)w;

                data++;// = data++;
            }

            int x = (sum2 << 8) | sum1;
            return (UInt16)x;
        }

        private void UpdateIndicator(Ellipse ellipse, TextBlock text, int percent)
        {
            // Atualiza texto
            text.Text = $"{percent}%";

            // Atualiza "preenchimento" (usando opacidade)
            ellipse.Opacity = percent / 100.0;

            // 🔥 EXTRA: efeito visual mais bonito (tipo força)
            ellipse.StrokeThickness = 8 + (percent * 0.1);
        }
    }
}