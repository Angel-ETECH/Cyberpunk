using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Diagnostics;
using System.Threading;
using System.Reflection;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Edgerunner {
 static class Program {
  public static string Home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"EdgerunnerOS");
  public static string Exe = Assembly.GetExecutingAssembly().Location;
  public static void Log(string text) { try { Directory.CreateDirectory(Home); File.AppendAllText(Path.Combine(Home,"desktop.log"),DateTime.Now.ToString("s")+" "+text+Environment.NewLine); } catch {} }
  [STAThread] public static int Main(string[] args) {
   try {
    if(args.Contains("--self-test")) return SelfTest(args);
    if(args.Contains("--diagnostics")) { try { using(var e=EventWaitHandle.OpenExisting("Local\\EdgerunnerOS.Capture")) e.Set(); } catch{} return 0; }
    if(args.Contains("--stop")) { try { using(var e=EventWaitHandle.OpenExisting("Local\\EdgerunnerOS.Stop")) e.Set(); } catch{} return 0; }
    bool owned; using(var mutex=new Mutex(true,"Local\\EdgerunnerOS.Nightfall",out owned)) {
     if(!owned) { try { using(var e=EventWaitHandle.OpenExisting("Local\\EdgerunnerOS.Show")) e.Set(); } catch{} return 0; }
     var app=new DesktopApp(); app.RunDesktop(args.Contains("--desktop")); return 0;
    }
   } catch(Exception e) { Log(e.ToString()); MessageBox.Show("No se pudo iniciar Edgerunner.\n"+e.Message,"EDGERUNNER",MessageBoxButton.OK,MessageBoxImage.Warning); return 1; }
  }
  static int SelfTest(string[] args) {
   Directory.CreateDirectory(Home);
   var app=new Application(); var settings=new Preferences(); var view=new DesktopView(settings,null,true); var stats=new Stats(); stats.Update(); Thread.Sleep(1100); stats.Update(); view.Update(stats);var mediaProbe=new MediaBridge();var song=Task.Run(()=>mediaProbe.Read()).GetAwaiter().GetResult();view.UpdateMedia(song);
   view.Root.Width=1600;view.Root.Height=900;view.Root.Measure(new Size(1600,900));view.Root.Arrange(new Rect(0,0,1600,900));view.Root.UpdateLayout();
   var bmp=new RenderTargetBitmap(1600,900,96,96,PixelFormats.Pbgra32);bmp.Render(view.Root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));
   string output=args.Length>1?Path.GetFullPath(args[1]):Path.Combine(Home,"preview.png");using(var f=File.Create(output))encoder.Save(f);
   if(stats.Ram<0||stats.Ram>100||stats.Cpu<0||stats.Cpu>100)throw new Exception("Invalid live telemetry");
   string report="RENDER OK\nCPU="+stats.Cpu+"\nRAM="+stats.Ram+"\nRAM_GB="+stats.RamDetail+"\nBATTERY="+stats.Battery+"\nDISK="+stats.Disk+"\nNETWORK="+stats.Network+"\n";
   var p=new Preferences();p.Theme="netrunner"; p.Animation=false;string temporary=Path.Combine(Path.GetDirectoryName(output),"settings-test.xml");p.Save(temporary);var loaded=Preferences.Load(temporary);if(loaded.Theme!="netrunner"||loaded.Animation)throw new Exception("Preferences roundtrip failed");File.Delete(temporary); report+="PREFERENCES ROUNDTRIP OK\n";
   if(Stats.Rate(2000,1000,2)!=500||Stats.Rate(1,1000,2)!=0)throw new Exception("Network counter reset failed");
   var focusTest=new FocusSession();focusTest.Toggle();if(!focusTest.Running)throw new Exception("Focus start failed");focusTest.Toggle();if(focusTest.Running||focusTest.Remaining<=0)throw new Exception("Focus pause failed");focusTest.Reset();if(focusTest.Clock!="25:00")throw new Exception("Focus reset failed");focusTest.Remaining=0.001;focusTest.Toggle();Thread.Sleep(10);if(!focusTest.Update()||focusTest.Running||focusTest.Clock!="00:00")throw new Exception("Focus completion failed");
   p.ShortcutTarget=@"C:\Program Files\Demo\demo.exe";p.ShortcutName="Demo";p.Save(temporary);loaded=Preferences.Load(temporary);if(loaded.ShortcutTarget!=p.ShortcutTarget)throw new Exception("Shortcut persistence failed");File.Delete(temporary);
   report+="WINDOWS MEDIA QUERY OK; SESSION="+song.Available+"; COVER="+(song.Cover!=null)+"; THUMB="+song.ThumbnailProvided+"; ART_ERROR="+song.ArtError+"\n";var peakProbe=new SpeakerMeter();Thread.Sleep(1550);double measuredPeak=peakProbe.Read();peakProbe.Dispose();if(measuredPeak<0||measuredPeak>1)throw new Exception("Invalid peak");report+="AUDIO PEAK QUERY OK\n";
   report+="NETWORK RATE / RESET OK\nFOCUS START / PAUSE / RESET / COMPLETION OK\nSHORTCUT PERSISTENCE OK\n";
   File.WriteAllText(Path.ChangeExtension(output,".txt"),report); app.Shutdown();return 0;
  }
 }
 class Preferences {
  public string Theme="edgerunners";public bool Animation=true,PauseOnBattery=true,Clock=true,Telemetry=true;
  public bool Music=true;
  public string ShortcutName="Mi aplicación",ShortcutTarget="";
  public static string FilePath {get{return Path.Combine(Program.Home,"settings.xml");}}
  public static Preferences Load(string file) {var p=new Preferences();try{var x=XDocument.Load(file).Root; p.ShortcutName=(string)x.Element("ShortcutName")??"Mi aplicación";p.ShortcutTarget=(string)x.Element("ShortcutTarget")??""; p.Music=(bool?)x.Element("Music")??true;p.Theme=(string)x.Element("Theme")??"edgerunners";p.Animation=(bool?)x.Element("Animation")??true;p.PauseOnBattery=(bool?)x.Element("PauseOnBattery")??true;p.Clock=(bool?)x.Element("Clock")??true;p.Telemetry=(bool?)x.Element("Telemetry")??true;}catch{}if(!new[]{"edgerunners","netrunner","arasaka"}.Contains(p.Theme))p.Theme="edgerunners";return p;}
  public void Save(string file){Directory.CreateDirectory(Path.GetDirectoryName(file));new XDocument(new XElement("Settings",new XElement("Music",Music),new XElement("ShortcutName",ShortcutName),new XElement("ShortcutTarget",ShortcutTarget),new XElement("Theme",Theme),new XElement("Animation",Animation),new XElement("PauseOnBattery",PauseOnBattery),new XElement("Clock",Clock),new XElement("Telemetry",Telemetry))).Save(file);}
  public Color Accent { get { return (Color)ColorConverter.ConvertFromString(Theme=="netrunner"?"#73E8DD":Theme=="arasaka"?"#FF727F":"#EAFF5A"); } }
 }
 class Stats {
  public double Cpu,Ram;public string RamDetail="",Disk="—",Battery="—",Network="RED LOCAL";public bool OnBattery;
  public List<double> History=new List<double>();public double Download,Upload;public string Uptime="";
  Dictionary<string,long[]> networkPrevious=new Dictionary<string,long[]>();Stopwatch networkClock=Stopwatch.StartNew();
  public static double Rate(long current,long previous,double seconds){return seconds>0&&current>=previous?(current-previous)/seconds:0;}
  public static string Speed(double value){return value>=1048576?(value/1048576).ToString("0.0")+" MB/s":(value/1024).ToString("0.0")+" KB/s";}
  ulong idleOld,kernelOld,userOld;
  public void Update() {
   Native.FileTime idle,kernel,user;
   if(Native.GetSystemTimes(out idle,out kernel,out user)){ulong i=idle.Value,k=kernel.Value,u=user.Value;ulong total=(k-kernelOld)+(u-userOld);if(kernelOld>0&&total>0)Cpu=Math.Max(0,Math.Min(100,100.0*(total-(i-idleOld))/total));idleOld=i;kernelOld=k;userOld=u;}
   var mem=new Native.Memory();mem.length=(uint)Marshal.SizeOf(mem);if(Native.GlobalMemoryStatusEx(ref mem)){Ram=mem.load;RamDetail=((mem.totalPhys-mem.availPhys)/1073741824.0).ToString("0.0",CultureInfo.InvariantCulture)+" / "+(mem.totalPhys/1073741824.0).ToString("0",CultureInfo.InvariantCulture)+" GB";}
   try{var drive=new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));Disk=drive.Name.Substring(0,2)+"  "+(100.0*(drive.TotalSize-drive.AvailableFreeSpace)/drive.TotalSize).ToString("0")+"% OCUPADO";}catch{Disk="DISCO NO DISPONIBLE";}
   var power=Forms.SystemInformation.PowerStatus;OnBattery=power.PowerLineStatus==Forms.PowerLineStatus.Offline;
   if((power.BatteryChargeStatus & Forms.BatteryChargeStatus.NoSystemBattery)!=0)Battery="CORRIENTE";else if(power.BatteryLifePercent>=0&&power.BatteryLifePercent<=1)Battery=(OnBattery?"BAT ":"AC ")+(power.BatteryLifePercent*100).ToString("0")+"%";else Battery="BATERÍA N/D";
   History.Add(Cpu);if(History.Count>60)History.RemoveAt(0);
   var elapsed=networkClock.Elapsed.TotalSeconds;networkClock.Restart();Download=Upload=0;
   var next=new Dictionary<string,long[]>();
   try{foreach(var adapter in NetworkInterface.GetAllNetworkInterfaces()){
    if(adapter.OperationalStatus!=OperationalStatus.Up||adapter.NetworkInterfaceType==NetworkInterfaceType.Loopback||adapter.NetworkInterfaceType==NetworkInterfaceType.Tunnel)continue;
    var data=adapter.GetIPv4Statistics();long[] previous;if(networkPrevious.TryGetValue(adapter.Id,out previous)){Download+=Rate(data.BytesReceived,previous[0],elapsed);Upload+=Rate(data.BytesSent,previous[1],elapsed);}next[adapter.Id]=new long[]{data.BytesReceived,data.BytesSent};
   }}catch{}networkPrevious=next;
   var uptime=TimeSpan.FromMilliseconds(Native.GetTickCount64());Uptime=((int)uptime.TotalHours).ToString("00")+"h "+uptime.Minutes.ToString("00")+"m";
   try{Network=NetworkInterface.GetIsNetworkAvailable()?"RED CONECTADA":"SIN CONEXIÓN DE RED";}catch{Network="RED NO DISPONIBLE";}
  }
 }
 class FocusSession {
  public bool Running;public double Remaining=1500;double deadline;
  public static double Now {get{return Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;}}
  public void Toggle(){if(Running){Update();Running=false;}else{if(Remaining<=0)Remaining=1500;deadline=Now+Remaining;Running=true;}}
  public bool Update(){if(!Running)return false;Remaining=Math.Max(0,deadline-Now);if(Remaining<=0){Running=false;return true;}return false;}
  public void Reset(){Running=false;Remaining=1500;}
  public string Clock {get{int sec=(int)Math.Ceiling(Remaining);return (sec/60).ToString("00")+":"+(sec%60).ToString("00");}}
 }
 class OutputMeter:FrameworkElement {
  public double Peak;
  // A decorative bar silhouette scales with one real endpoint peak; not an FFT.
  public static double BarHeight(int i,double peak){return 2+Math.Sqrt(Math.Max(0,Math.Min(1,peak)))*(27+((i*31)%53));}
  protected override void OnRender(DrawingContext d){for(int i=0;i<32;i++){double h=BarHeight(i,Peak);d.DrawRectangle(new SolidColorBrush(Color.FromRgb((byte)(120+i*3),220,90)),null,new Rect(i*14,90-h,10,h));}}
 }
 class RainLayer:FrameworkElement {
  double phase; public bool Active=true;
  public void Step(){phase+=0.8;InvalidateVisual();}
  protected override void OnRender(DrawingContext d) {
   if(!Active)return;double w=ActualWidth,h=ActualHeight;var p=new Pen(new SolidColorBrush(Color.FromArgb(31,170,221,240)),0.7);p.Freeze();
   for(int i=0;i<65;i++){double x=((i*163.73-phase*1.3)%(w+80)+w+80)%(w+80);double y=(i*83.9+phase*(2.2+(i%5)*.3))%(h+50);d.DrawLine(p,new Point(x,y),new Point(x-4,y+14+(i%4)*3));}
  }
 }
 // A classic redirected GDI surface is composited reliably by Explorer 24H2.
 // WPF remains responsible for layout; its pixels are copied once per second.
 // Rain is painted separately, avoiding a full WPF render on every animation frame.
 class DesktopSurface:Forms.Form {
  IntPtr ownerParent;int pixelWidth,pixelHeight;System.Drawing.Bitmap frame,displayFrame;double phase;
  public bool Animation=true,ShowMeter=true;public double Peak;
  public DesktopSurface(IntPtr parent,int width,int height){ownerParent=parent;pixelWidth=width;pixelHeight=height;FormBorderStyle=Forms.FormBorderStyle.None;ShowInTaskbar=false;Text="EDGERUNNER / Desktop";DoubleBuffered=true;SetStyle(Forms.ControlStyles.AllPaintingInWmPaint|Forms.ControlStyles.UserPaint|Forms.ControlStyles.OptimizedDoubleBuffer,true);var handle=Handle;}
  protected override Forms.CreateParams CreateParams {get{var p=base.CreateParams;p.Parent=ownerParent;p.Style=(p.Style&~unchecked((int)0x80000000))|0x40000000|0x10000000;p.ExStyle|=0x00000080|0x00000020|0x08000000;p.Width=pixelWidth;p.Height=pixelHeight;return p;}}
  public void Render(Grid root){int w=1600,h=(int)Math.Round(1600.0*pixelHeight/pixelWidth);root.Width=w;root.Height=h;root.Measure(new Size(w,h));root.Arrange(new Rect(0,0,w,h));root.UpdateLayout();var audio=root.FindName("AudioHost") as Grid;if(audio!=null)audio.Visibility=Visibility.Collapsed;var rain=root.FindName("RainHost") as Grid;if(rain!=null)rain.Visibility=Visibility.Collapsed;var bmp=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);bmp.Render(root);if(frame==null||frame.Width!=w||frame.Height!=h){if(frame!=null)frame.Dispose();frame=new System.Drawing.Bitmap(w,h,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);}var data=frame.LockBits(new System.Drawing.Rectangle(0,0,w,h),System.Drawing.Imaging.ImageLockMode.WriteOnly,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);try{bmp.CopyPixels(Int32Rect.Empty,data.Scan0,data.Stride*h,data.Stride);}finally{frame.UnlockBits(data);}if(displayFrame==null)displayFrame=new System.Drawing.Bitmap(pixelWidth,pixelHeight,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);using(var g=System.Drawing.Graphics.FromImage(displayFrame)){g.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;g.DrawImage(frame,new System.Drawing.Rectangle(0,0,pixelWidth,pixelHeight));}Invalidate();}
  public void Step(){phase+=0.8;Invalidate();}
  protected override void OnPaint(Forms.PaintEventArgs e){e.Graphics.Clear(System.Drawing.Color.FromArgb(7,15,24));if(displayFrame!=null)e.Graphics.DrawImageUnscaled(displayFrame,0,0);if(ShowMeter){double scale=Math.Min(ClientSize.Width/1600.0,ClientSize.Height/900.0),ox=(ClientSize.Width-1600*scale)/2,oy=(ClientSize.Height-900*scale)/2;for(int i=0;i<32;i++){double height=OutputMeter.BarHeight(i,Peak);using(var brush=new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(230,120+(int)(i/31.0*105),220,90))){e.Graphics.FillRectangle(brush,(float)(ox+(577+i*14)*scale),(float)(oy+(542-height)*scale),(float)(10*scale),(float)(height*scale));}}}if(Animation){using(var pen=new System.Drawing.Pen(System.Drawing.Color.FromArgb(31,170,221,240),0.7f)){int w=ClientSize.Width,h=ClientSize.Height;for(int i=0;i<65;i++){float x=(float)(((i*163.73-phase*1.3)%(w+80)+w+80)%(w+80));float y=(float)((i*83.9+phase*(2.2+(i%5)*.3))%(h+50));e.Graphics.DrawLine(pen,x,y,x-4,y+14+(i%4)*3);}}}}
  protected override void Dispose(bool disposing){if(disposing&&frame!=null){frame.Dispose();frame=null;}if(disposing&&displayFrame!=null){displayFrame.Dispose();displayFrame=null;}base.Dispose(disposing);}
 }
 class DesktopApp:Application {
  Preferences settings=Preferences.Load(Preferences.FilePath);Stats stats=new Stats();FocusSession focus=new FocusSession();MediaBridge media=new MediaBridge();SpeakerMeter speaker=new SpeakerMeter();Forms.ToolStripMenuItem customItem;Window preview,options;DesktopView previewView,desktopView;DesktopSurface desktop;IntPtr parent;Forms.NotifyIcon tray;DispatcherTimer tick,animation;
  EventWaitHandle showEvent,stopEvent,captureEvent;RegisteredWaitHandle showWait,stopWait,captureWait;bool exiting=false,manuallyPaused=false,applying=false;int retries;System.Drawing.Rectangle desktopBounds;string desktopStatus="Vista previa. Aún no aplicado al escritorio.";
  public void RunDesktop(bool activate){ShutdownMode=ShutdownMode.OnExplicitShutdown;DispatcherUnhandledException+=(s,e)=>{Program.Log(e.Exception.ToString());MessageBox.Show(e.Exception.Message,"EDGERUNNER");e.Handled=true;};SetupTray();SetupSignals();SystemEvents.DisplaySettingsChanged+=DisplayChanged;Exit+=(s,e)=>Cleanup();tick=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};tick.Tick+=(s,e)=>Refresh();tick.Start();animation=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(66)};animation.Tick+=(s,e)=>Animate();animation.Start();stats.Update();Startup+=(s,e)=>{if(activate)ApplyDesktop();else ShowPreview();};Run();}
  void SetupSignals(){showEvent=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\EdgerunnerOS.Show");stopEvent=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\EdgerunnerOS.Stop");captureEvent=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\EdgerunnerOS.Capture");showWait=ThreadPool.RegisterWaitForSingleObject(showEvent,(s,t)=>Dispatcher.BeginInvoke(new Action(ShowPreview)),null,-1,false);stopWait=ThreadPool.RegisterWaitForSingleObject(stopEvent,(s,t)=>Dispatcher.BeginInvoke(new Action(ExitApp)),null,-1,false);captureWait=ThreadPool.RegisterWaitForSingleObject(captureEvent,(s,t)=>Dispatcher.BeginInvoke(new Action(CaptureDesktop)),null,-1,false);}
  void CaptureDesktop(){if(desktopView==null||desktop==null)return;var root=desktopView.Root;Directory.CreateDirectory(Program.Home);using(var bitmap=new System.Drawing.Bitmap(desktop.ClientSize.Width,desktop.ClientSize.Height)){desktop.DrawToBitmap(bitmap,new System.Drawing.Rectangle(0,0,desktop.ClientSize.Width,desktop.ClientSize.Height));bitmap.Save(Path.Combine(Program.Home,"desktop-render.png"),System.Drawing.Imaging.ImageFormat.Png);}Native.RECT rect;Native.GetWindowRect(desktop.Handle,out rect);File.WriteAllText(Path.Combine(Program.Home,"desktop-state.txt"),"Child="+desktop.Handle+"\nParent="+Native.GetParent(desktop.Handle)+"\nParentAlive="+Native.IsWindow(parent)+"\nVisible="+Native.IsWindowVisible(desktop.Handle)+"\nRoot="+root.ActualWidth+"x"+root.ActualHeight+"\nBounds="+rect.Left+","+rect.Top+","+rect.Right+","+rect.Bottom+"\nAnimation="+desktopView.Rain.Active+"\nStartup="+StartupEnabled()+"\nMediaAvailable="+media.Current.Available+"\nArtwork="+(media.Current.Cover!=null)+"\nOutputPeak="+desktop.Peak);}
  void SetupTray(){tray=new Forms.NotifyIcon();using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("edgerunner.ico"))tray.Icon=new System.Drawing.Icon(stream);tray.Text="EDGERUNNER · Nightfall";var menu=new Forms.ContextMenuStrip();menu.Items.Add("Personalizar…",null,(s,e)=>Dispatcher.Invoke(new Action(OpenSettings)));menu.Items.Add("Ver diseño",null,(s,e)=>Dispatcher.Invoke(new Action(ShowPreview)));menu.Items.Add("Activar en el escritorio",null,(s,e)=>Dispatcher.Invoke(new Action(ApplyDesktop)));menu.Items.Add("Pausar / reanudar lluvia",null,(s,e)=>{manuallyPaused=!manuallyPaused;});menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add("Explorador de archivos",null,(s,e)=>OpenTarget("explorer.exe"));menu.Items.Add("Navegador",null,(s,e)=>OpenTarget("https://www.google.com"));menu.Items.Add("Configuración de Windows",null,(s,e)=>OpenTarget("ms-settings:"));menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add("Enfoque · iniciar / pausar 25 min",null,(s,e)=>Dispatcher.Invoke(new Action(ToggleFocus)));menu.Items.Add("Música · anterior",null,(s,e)=>MediaControl("previous"));menu.Items.Add("Música · reproducir / pausar",null,(s,e)=>MediaControl("toggle"));menu.Items.Add("Música · siguiente",null,(s,e)=>MediaControl("next"));menu.Items.Add("Enfoque · reiniciar",null,(s,e)=>Dispatcher.Invoke(new Action(ResetFocus)));customItem=new Forms.ToolStripMenuItem("Abrir "+settings.ShortcutName,null,(s,e)=>Dispatcher.Invoke(new Action(LaunchShortcut)));menu.Items.Add(customItem);menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add("Salir y restaurar escritorio",null,(s,e)=>Dispatcher.Invoke(new Action(ExitApp)));tray.ContextMenuStrip=menu;tray.DoubleClick+=(s,e)=>Dispatcher.Invoke(new Action(OpenSettings));tray.Visible=true;}
  public void MediaControl(string command){media.Control(command);}
  public void ToggleFocus(){focus.Toggle();}
  void ResetFocus(){focus.Reset();}
  public void ConfigureShortcut(){var dialog=new Forms.OpenFileDialog{Title="Seleccionar aplicación o archivo",Filter="Aplicaciones (*.exe)|*.exe|Todos los archivos (*.*)|*.*"};if(dialog.ShowDialog()!=Forms.DialogResult.OK)return;settings.ShortcutTarget=dialog.FileName;settings.ShortcutName=Path.GetFileNameWithoutExtension(dialog.FileName);settings.Save(Preferences.FilePath);if(customItem!=null)customItem.Text="Abrir "+settings.ShortcutName;}
  void LaunchShortcut(){if(File.Exists(settings.ShortcutTarget))OpenTarget(settings.ShortcutTarget);else{MessageBox.Show("Selecciona primero tu aplicación desde Personalizar → Elegir acceso rápido.","Acceso rápido");}}
  static void OpenTarget(string target){try{Process.Start(new ProcessStartInfo(target){UseShellExecute=true});}catch(Exception e){MessageBox.Show(e.Message,"No se pudo abrir");}}
  public void ShowPreview(){if(exiting)return;if(preview!=null){preview.Show();preview.Activate();return;}previewView=new DesktopView(settings,this,true);preview=new Window{Title="EDGERUNNER / NIGHTFALL — Vista previa",Content=previewView.Root,Width=1180,Height=700,MinWidth=800,MinHeight=500,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=Brushes.Black};using(var st=Assembly.GetExecutingAssembly().GetManifestResourceStream("edgerunner.ico"))preview.Icon=BitmapFrame.Create(st);preview.Closed+=(s,e)=>{preview=null;previewView=null;};preview.Show();previewView.Update(stats);previewView.UpdateFocus(focus);previewView.UpdateMedia(media.Current);}
  public void ApplyDesktop(){if(exiting||applying)return;applying=true;try{if(desktop==null||!Native.IsWindow(parent)){DisposeDesktop();var screen=Forms.Screen.PrimaryScreen.Bounds;desktopBounds=screen;parent=Native.DesktopParent();if(parent==IntPtr.Zero)throw new Exception("Windows no expuso una capa de fondo compatible. No se modificó tu fondo actual.");var origin=new Native.POINT{X=screen.X,Y=screen.Y};Native.MapWindowPoints(IntPtr.Zero,parent,ref origin,1);desktopView=new DesktopView(settings,this,false);desktopView.Update(stats);desktopView.UpdateFocus(focus);desktopView.UpdateMedia(media.Current);desktop=new DesktopSurface(parent,screen.Width,screen.Height);desktop.ShowMeter=settings.Music;desktop.Render(desktopView.Root);desktop.Show();Native.PlaceDesktop(desktop.Handle,parent,origin.X,origin.Y,screen.Width,screen.Height);desktop.Invalidate();desktop.Update();Program.Log("Desktop active. Renderer=GDI Layered="+Native.LayeredDesktop+" parent="+parent+" child="+desktop.Handle+" size="+screen.Width+"x"+screen.Height);}
   desktopStatus="Activo en tu monitor principal.";retries=0;if(preview!=null)preview.Close();tray.ShowBalloonTip(3500,"EDGERUNNER activo","Ajustes y accesos: icono E junto al reloj de Windows.",Forms.ToolTipIcon.Info);
  }catch(Exception ex){Program.Log(ex.ToString());DisposeDesktop();desktopStatus=ex.Message;ShowPreview();MessageBox.Show("No pude aplicar el fondo.\n\n"+ex.Message+"\n\nLa vista previa sigue disponible.","EDGERUNNER",MessageBoxButton.OK,MessageBoxImage.Information);}finally{applying=false;}}
  void DisposeDesktop(){if(desktop!=null){try{desktop.Dispose();}catch{}desktop=null;}desktopView=null;parent=IntPtr.Zero;}
  void Refresh(){stats.Update();if(settings.Music)media.Tick();if(focus.Update())tray.ShowBalloonTip(5000,"Sesión terminada","Terminaste tus 25 minutos de enfoque. Toma un descanso.",Forms.ToolTipIcon.Info);if(previewView!=null){previewView.Update(stats);previewView.UpdateFocus(focus);previewView.UpdateMedia(media.Current);}if(desktopView!=null){desktopView.Update(stats);desktopView.UpdateFocus(focus);desktopView.UpdateMedia(media.Current);if(desktop!=null&&!desktop.IsDisposed)desktop.ShowMeter=settings.Music;desktop.Render(desktopView.Root);}if(desktop!=null&&(!Native.IsWindow(parent)||desktop.IsDisposed||!Native.IsWindow(desktop.Handle))){DisposeDesktop();if(retries++<3)ApplyDesktop();else ShowPreview();}}
  bool Fullscreen(){IntPtr fg=Native.GetForegroundWindow();if(fg==IntPtr.Zero||fg==parent||fg==Native.FindWindow("Progman",null))return false;var b=new StringBuilder(128);Native.GetClassName(fg,b,b.Capacity);if(b.ToString()=="WorkerW"||b.ToString()=="Progman")return false;Native.RECT rect;if(!Native.GetWindowRect(fg,out rect))return false;var bounds=Forms.Screen.PrimaryScreen.Bounds;return rect.Left<=bounds.Left&&rect.Top<=bounds.Top&&rect.Right>=bounds.Right&&rect.Bottom>=bounds.Bottom;}
  void Animate(){bool active=settings.Animation&&!manuallyPaused&&!(settings.PauseOnBattery&&stats.OnBattery);double peak=settings.Music&&!Fullscreen()?speaker.Read():0;if(previewView!=null)previewView.UpdatePeak(peak);if(desktop!=null)desktop.Peak=peak;if(previewView!=null){bool a=active&&preview.IsVisible&&preview.WindowState!=WindowState.Minimized;if(previewView.Rain.Active!=a){previewView.Rain.Active=a;previewView.Rain.InvalidateVisual();}if(a)previewView.Rain.Step();}if(desktop!=null&&!desktop.IsDisposed){bool a=active&&!Fullscreen();if(desktop.Animation!=a){desktop.Animation=a;desktop.Invalidate();}if(a)desktop.Step();}}
  void DisplayChanged(object s,EventArgs e){Dispatcher.BeginInvoke(new Action(()=>{if(!exiting&&!applying&&desktop!=null&&Forms.Screen.PrimaryScreen.Bounds!=desktopBounds){DisposeDesktop();ApplyDesktop();}}));}
  public static bool StartupEnabled(){using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){var v=key==null?null:key.GetValue("EdgerunnerOS") as string;return v=="\""+Program.Exe+"\" --desktop";}}
  static void SetStartup(bool enabled){using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){if(enabled)key.SetValue("EdgerunnerOS","\""+Program.Exe+"\" --desktop");else key.DeleteValue("EdgerunnerOS",false);}}
  TextBlock Label(string text,int size,Brush color){return new TextBlock{Text=text,FontFamily=new FontFamily("Segoe UI"),FontSize=size,Foreground=color,TextWrapping=TextWrapping.Wrap};}
  public void OpenSettings(){if(exiting)return;if(options!=null){options.Show();options.Activate();return;}var window=new Window{Title="EDGERUNNER / Personalizar",Width=490,Height=790,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=new SolidColorBrush(Color.FromRgb(9,20,31))};options=window;var wrap=new StackPanel{Margin=new Thickness(32)};window.Content=new ScrollViewer{Content=wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};wrap.Children.Add(Label("NIGHTFALL / PERSONALIZAR",11,new SolidColorBrush(settings.Accent)));var title=Label("Hazlo tuyo.",32,Brushes.White);title.Margin=new Thickness(0,10,0,6);wrap.Children.Add(title);var subtitle=Label("Una ciudad para desconectarte. Un escritorio para seguir creando.",13,Brushes.LightSlateGray);subtitle.Margin=new Thickness(0,0,0,24);wrap.Children.Add(subtitle);wrap.Children.Add(Label("COLOR DE LOS PANELES",10,Brushes.LightSteelBlue));var themes=new ComboBox{Margin=new Thickness(0,9,0,18),Height=34,FontSize=13,ItemsSource=new[]{"Edgerunners · amarillo ácido","Netrunner · turquesa","Arasaka · coral"},SelectedIndex=settings.Theme=="netrunner"?1:settings.Theme=="arasaka"?2:0};wrap.Children.Add(themes);
   Action persist=()=>{try{settings.Save(Preferences.FilePath);if(previewView!=null)previewView.ApplyPreferences();if(desktopView!=null)desktopView.ApplyPreferences();}catch(Exception e){MessageBox.Show(e.Message,"No se guardaron los ajustes");}};
   themes.SelectionChanged+=(s,e)=>{settings.Theme=new[]{"edgerunners","netrunner","arasaka"}[Math.Max(0,themes.SelectedIndex)];persist();};
   AddCheck(wrap,"Lluvia animada",settings.Animation,v=>{settings.Animation=v;persist();});AddCheck(wrap,"Pausar lluvia al usar batería",settings.PauseOnBattery,v=>{settings.PauseOnBattery=v;persist();});AddCheck(wrap,"Mostrar reloj y fecha",settings.Clock,v=>{settings.Clock=v;persist();});AddCheck(wrap,"Mostrar datos del equipo",settings.Telemetry,v=>{settings.Telemetry=v;persist();});AddCheck(wrap,"Mostrar música y medidor de salida",settings.Music,v=>{settings.Music=v;persist();});AddCheck(wrap,"Iniciar con Windows",StartupEnabled(),v=>{try{SetStartup(v);}catch(Exception e){MessageBox.Show("No se pudo cambiar el inicio automático.\n"+e.Message);}});
   var actions=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,6,0,12)};
   var focusButton=new Button{Content="Enfoque: iniciar / pausar",Padding=new Thickness(10),Margin=new Thickness(0,0,8,0)};focusButton.Click+=(s,e)=>ToggleFocus();actions.Children.Add(focusButton);
   var shortcutButton=new Button{Content="Elegir acceso rápido",Padding=new Thickness(10)};shortcutButton.Click+=(s,e)=>ConfigureShortcut();actions.Children.Add(shortcutButton);wrap.Children.Add(actions);
   var info=Label("Gráfica CPU: últimas 60 muestras. Red: tráfico de interfaces activas, no velocidad contratada. El enfoque no bloquea programas.",11,Brushes.LightSlateGray);info.Margin=new Thickness(0,0,0,12);wrap.Children.Add(info);
   var status=Label(desktopStatus,11,Brushes.LightSlateGray);status.Margin=new Thickness(0,13,0,12);wrap.Children.Add(status);
   var apply=new Button{Content="Activar en mi escritorio  ↗",Padding=new Thickness(14),Background=new SolidColorBrush(settings.Accent),Foreground=Brushes.Black,BorderThickness=new Thickness(0),FontSize=14,FontWeight=FontWeights.SemiBold};apply.Click+=(s,e)=>{ApplyDesktop();status.Text=desktopStatus;};wrap.Children.Add(apply);
   var note=Label("Controla el fondo desde el icono E de la bandeja. Al salir, tu escritorio anterior vuelve a verse. No muevas el .exe si activas el inicio automático.",11,Brushes.SlateGray);note.Margin=new Thickness(0,15,0,0);wrap.Children.Add(note);window.Closed+=(s,e)=>options=null;window.Show();}
  void AddCheck(StackPanel parent,string text,bool initial,Action<bool> action){var check=new CheckBox{Content=text,IsChecked=initial,Foreground=Brushes.Gainsboro,FontSize=13,Margin=new Thickness(0,0,0,14)};check.Checked+=(s,e)=>action(true);check.Unchecked+=(s,e)=>action(false);parent.Children.Add(check);}
  public void ExitApp(){if(exiting)return;exiting=true;Shutdown();}
  void Cleanup(){if(tick!=null)tick.Stop();if(animation!=null)animation.Stop();SystemEvents.DisplaySettingsChanged-=DisplayChanged;speaker.Dispose();DisposeDesktop();if(tray!=null){tray.Visible=false;tray.Dispose();}if(showWait!=null)showWait.Unregister(null);if(stopWait!=null)stopWait.Unregister(null);if(captureWait!=null)captureWait.Unregister(null);if(showEvent!=null)showEvent.Dispose();if(stopEvent!=null)stopEvent.Dispose();if(captureEvent!=null)captureEvent.Dispose();}
 }
 class DesktopView {
  public Grid Root;public RainLayer Rain;public Preferences Settings;public bool Preview;
  T Named<T>(string name) where T:class{return Root.FindName(name) as T;}
  static BitmapImage backdrop;public OutputMeter Meter;
  public DesktopView(Preferences settings,DesktopApp owner,bool preview) {
   Settings=settings;Preview=preview;
   using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("Interface.xaml"))Root=(Grid)XamlReader.Load(s);
   if(backdrop==null){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("night-city.png")){backdrop=new BitmapImage();backdrop.BeginInit();backdrop.CacheOption=BitmapCacheOption.OnLoad;backdrop.StreamSource=s;backdrop.EndInit();backdrop.Freeze();}}
   Named<System.Windows.Controls.Image>("Scene").Source=backdrop;
   Rain=new RainLayer();Named<Grid>("RainHost").Children.Add(Rain);
   if(owner!=null){Named<Button>("SettingsButton").Click+=(s,e)=>owner.OpenSettings();Named<Button>("ApplyButton").Click+=(s,e)=>owner.ApplyDesktop();Named<Button>("ExitButton").Click+=(s,e)=>owner.ExitApp();Named<Button>("PreviousButton").Click+=(s,e)=>owner.MediaControl("previous");Named<Button>("PlayButton").Click+=(s,e)=>owner.MediaControl("toggle");Named<Button>("NextButton").Click+=(s,e)=>owner.MediaControl("next");}
   if(!preview){Named<StackPanel>("PreviewActions").Visibility=Visibility.Collapsed;Named<StackPanel>("MusicButtons").Visibility=Visibility.Collapsed;Named<Button>("SettingsButton").Visibility=Visibility.Collapsed;Named<TextBlock>("ModeLabel").Text="NEURAL DESKTOP / 03";Named<TextBlock>("ModeHint").Text="Tu escritorio. Tu ritmo.";Named<TextBlock>("DesktopHint").Visibility=Visibility.Visible;}
   Meter=new OutputMeter();Named<Grid>("AudioHost").Children.Add(Meter);BuildMap();
   ApplyPreferences();
  }
  public void ApplyPreferences(){Named<Grid>("MusicGroup").Visibility=Settings.Music?Visibility.Visible:Visibility.Collapsed;Root.Resources["Accent"]=new SolidColorBrush(Settings.Accent);Named<StackPanel>("ClockGroup").Visibility=Settings.Clock?Visibility.Visible:Visibility.Collapsed;Named<StackPanel>("TelemetryGroup").Visibility=Settings.Telemetry?Visibility.Visible:Visibility.Collapsed;Named<Grid>("NeuralGroup").Visibility=Settings.Telemetry?Visibility.Visible:Visibility.Collapsed;Rain.Active=Settings.Animation;Rain.InvalidateVisual();}
  void BuildMap(){var canvas=Named<Canvas>("CityMap");for(int r=0;r<4;r++)for(int c=0;c<5;c++){double x=120+(c-r)*23,y=37+(c+r)*9,h=10+(r*17+c*29)%52;var top=new System.Windows.Shapes.Polygon{Points=new PointCollection{new Point(x,y-h),new Point(x+18,y+8-h),new Point(x,y+16-h),new Point(x-18,y+8-h)},Fill=new SolidColorBrush(Color.FromRgb(76,164,177)),Stroke=new SolidColorBrush(Color.FromRgb(126,208,210)),StrokeThickness=.5};var side=new System.Windows.Shapes.Polygon{Points=new PointCollection{new Point(x-18,y+8-h),new Point(x,y+16-h),new Point(x,y+16),new Point(x-18,y+8)},Fill=new SolidColorBrush(Color.FromRgb(21,75,91)),Stroke=top.Stroke,StrokeThickness=.5};var right=new System.Windows.Shapes.Polygon{Points=new PointCollection{new Point(x,y+16-h),new Point(x+18,y+8-h),new Point(x+18,y+8),new Point(x,y+16)},Fill=new SolidColorBrush(Color.FromRgb(13,48,65)),Stroke=top.Stroke,StrokeThickness=.5};canvas.Children.Add(side);canvas.Children.Add(right);canvas.Children.Add(top);}}
  public void UpdatePeak(double peak){Meter.Peak=peak;Meter.InvalidateVisual();}
  public void UpdateMedia(MediaSnapshot item){Named<TextBlock>("MusicTitle").Text=item.Title;Named<TextBlock>("MusicArtist").Text=item.Artist;Named<TextBlock>("MusicStatus").Text=item.Status;Named<TextBlock>("MusicSource").Text=item.Source;Named<TextBlock>("MusicTime").Text=item.Duration>0?MediaBridge.Time(item.Position)+" / "+MediaBridge.Time(item.Duration):"— / —";Named<Border>("MusicProgress").Width=item.Duration>0?Math.Min(340,340*item.Position/item.Duration):0;Named<System.Windows.Controls.Image>("AlbumArt").Source=item.Cover;}
  public void UpdateFocus(FocusSession focus){Named<TextBlock>("FocusTime").Text=focus.Clock;Named<TextBlock>("FocusStatus").Text=focus.Running?"SESIÓN ACTIVA // CONCÉNTRATE":focus.Remaining<=0?"SESIÓN COMPLETADA":focus.Remaining<1500?"SESIÓN PAUSADA":"INICIA DESDE EL ICONO E";Named<Border>("FocusBar").Width=225*(1-focus.Remaining/1500);}
  public void Update(Stats s){Named<TextBlock>("SampleCount").Text="BUFFER / "+s.History.Count.ToString("00")+" DE 60";
   Named<TextBlock>("NetRate").Text="↓ "+Stats.Speed(s.Download)+"   ↑ "+Stats.Speed(s.Upload);
   Named<TextBlock>("Uptime").Text="ENCENDIDO  "+s.Uptime;
   Named<TextBlock>("ShortcutSummary").Text="EXPLORADOR / NAVEGADOR / "+Settings.ShortcutName.ToUpperInvariant();
   var points=new PointCollection();for(int i=0;i<s.History.Count;i++)points.Add(new Point(328.0*(i+60-s.History.Count)/59,43-s.History[i]*.41));Named<System.Windows.Shapes.Polyline>("CpuHistory").Points=points;
   var now=DateTime.Now;Named<TextBlock>("Hours").Text=now.ToString("HH");Named<TextBlock>("Minutes").Text=now.ToString("mm");Named<TextBlock>("Seconds").Text=now.ToString("ss");Named<TextBlock>("Date").Text=now.ToString("dddd, dd 'de' MMMM",CultureInfo.GetCultureInfo("es-PE"));Named<TextBlock>("CpuText").Text=s.Cpu.ToString("0")+"%";Named<Border>("CpuBar").Width=Math.Max(0,Math.Min(328,s.Cpu*3.28));Named<TextBlock>("RamText").Text=s.RamDetail;Named<Border>("RamBar").Width=Math.Max(0,Math.Min(328,s.Ram*3.28));Named<TextBlock>("DiskText").Text=s.Disk;Named<TextBlock>("BatteryText").Text=s.Battery;Named<TextBlock>("Network").Text=s.Network;}
 }
 static class Native {
  [StructLayout(LayoutKind.Sequential)]public struct FileTime{public uint low,high;public ulong Value{get{return ((ulong)high<<32)|low;}}}
  [StructLayout(LayoutKind.Sequential)]public struct Memory{public uint length,load;public ulong totalPhys,availPhys,totalPage,availPage,totalVirtual,availVirtual,availExtended;}
  [StructLayout(LayoutKind.Sequential)]public struct RECT{public int Left,Top,Right,Bottom;}
  [StructLayout(LayoutKind.Sequential)]public struct POINT{public int X,Y;}
  public delegate bool EnumProc(IntPtr hwnd,IntPtr lparam);
  [DllImport("kernel32.dll")]public static extern ulong GetTickCount64();
  [DllImport("kernel32.dll")]public static extern bool GetSystemTimes(out FileTime idle,out FileTime kernel,out FileTime user);
  [DllImport("kernel32.dll")]public static extern bool GlobalMemoryStatusEx(ref Memory mem);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr FindWindow(string cls,string title);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
  [DllImport("user32.dll")]public static extern bool EnumWindows(EnumProc proc,IntPtr arg);
  [DllImport("user32.dll")]public static extern IntPtr SendMessageTimeout(IntPtr h,uint msg,IntPtr wp,IntPtr lp,uint flags,uint timeout,out IntPtr result);
  [DllImport("user32.dll")]public static extern bool IsWindow(IntPtr h);
  [DllImport("user32.dll")]public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")]public static extern IntPtr GetParent(IntPtr h);
  [DllImport("user32.dll",SetLastError=true)]public static extern IntPtr SetParent(IntPtr h,IntPtr parent);
  [DllImport("user32.dll")]public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out RECT r);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern int GetClassName(IntPtr h,StringBuilder cls,int max);
  [DllImport("user32.dll")]public static extern int MapWindowPoints(IntPtr from,IntPtr to,ref POINT p,uint count);
  [DllImport("user32.dll")]public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int ht,uint flags);
  [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]public static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
  [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)]public static extern IntPtr SetWindowLongPtr(IntPtr h,int index,IntPtr value);
  [DllImport("user32.dll",SetLastError=true)]public static extern bool SetLayeredWindowAttributes(IntPtr h,uint color,byte alpha,uint flags);
  public static bool LayeredDesktop;static IntPtr iconView,wallpaperWorker;
  public static IntPtr DesktopParent(){IntPtr prog=FindWindow("Progman",null);if(prog==IntPtr.Zero)return IntPtr.Zero;
   LayeredDesktop=(GetWindowLongPtr(prog,-20).ToInt64()&0x00200000)!=0;
   IntPtr result;SendMessageTimeout(prog,0x052C,new IntPtr(0xD),IntPtr.Zero,2,1000,out result);SendMessageTimeout(prog,0x052C,new IntPtr(0xD),new IntPtr(1),2,1000,out result);
   SendMessageTimeout(prog,0x052C,IntPtr.Zero,IntPtr.Zero,2,1000,out result);
   iconView=FindWindowEx(prog,IntPtr.Zero,"SHELLDLL_DefView",null);
   wallpaperWorker=FindWindowEx(prog,IntPtr.Zero,"WorkerW",null);
   // In Windows 11 24H2+, the renderer must be a layered sibling of DefView,
   // below the icons but above WorkerW. A child of WorkerW may exist yet be invisible.
   if(LayeredDesktop&&iconView!=IntPtr.Zero)return prog;
   IntPtr worker=IntPtr.Zero;
   EnumWindows((top,l)=>{IntPtr view=FindWindowEx(top,IntPtr.Zero,"SHELLDLL_DefView",null);if(view!=IntPtr.Zero){worker=FindWindowEx(IntPtr.Zero,top,"WorkerW",null);return false;}return true;},IntPtr.Zero);return worker;
  }
  public static void PlaceDesktop(IntPtr h,IntPtr parent,int x,int y,int width,int height){
   // WinForms can park child Forms in its hidden parking window during Show().
   // Attach after that lifecycle step, then verify the actual native parent.
   long childStyle=(GetWindowLongPtr(h,-16).ToInt64()&~0x80C40000L)|0x56000000L;
   SetWindowLongPtr(h,-16,new IntPtr(childStyle));SetParent(h,parent);
   if(GetParent(h)!=parent)throw new Exception("Windows no conectó la ventana al escritorio.");
   if(LayeredDesktop){SetWindowLongPtr(h,-20,new IntPtr(GetWindowLongPtr(h,-20).ToInt64()|0x00080000));if(!SetLayeredWindowAttributes(h,0,255,2))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}
   if(!SetWindowPos(h,LayeredDesktop?iconView:IntPtr.Zero,x,y,width,height,0x0010|0x0040|0x0020))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
   if(LayeredDesktop&&wallpaperWorker!=IntPtr.Zero)SetWindowPos(wallpaperWorker,new IntPtr(1),0,0,0,0,0x0010|0x0001|0x0002);
  }
 }
}
