using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Edgerunner {
 sealed class MediaSnapshot {
  public string Title="Sin reproducción",Artist="Abre música en una aplicación compatible",Source="WINDOWS MEDIA",Status="EN ESPERA";
  public bool Available,Playing,ThumbnailProvided;public string ArtError="";public double Position,Duration;public BitmapSource Cover;
 }
 sealed class MediaBridge {
  GlobalSystemMediaTransportControlsSessionManager manager;
  MediaSnapshot previous=new MediaSnapshot();string previousKey="";Task<MediaSnapshot> pending;Stopwatch cadence=Stopwatch.StartNew();
  public MediaSnapshot Current=new MediaSnapshot();public string LastError="";
  public static T Result<T>(IAsyncOperation<T> op){var clock=Stopwatch.StartNew();try{while(op.Status==AsyncStatus.Started){if(clock.ElapsedMilliseconds>5000){op.Cancel();throw new TimeoutException("Windows Media timeout");}Thread.Sleep(20);}return op.GetResults();}finally{try{op.Close();}catch{}}}
  public static T Result<T,P>(IAsyncOperationWithProgress<T,P> op){var clock=Stopwatch.StartNew();try{while(op.Status==AsyncStatus.Started){if(clock.ElapsedMilliseconds>5000){op.Cancel();throw new TimeoutException();}Thread.Sleep(20);}return op.GetResults();}finally{try{op.Close();}catch{}}}
  public void Tick(){if(pending!=null&&pending.IsCompleted){try{Current=pending.GetAwaiter().GetResult();LastError="";}catch(Exception ex){LastError=ex.GetType().Name;Current=new MediaSnapshot{Title="Información no disponible",Artist="Windows no pudo leer la sesión multimedia"};manager=null;}pending=null;}if(pending==null&&(cadence.ElapsedMilliseconds>=1500)){cadence.Restart();pending=Task.Run(()=>Read());}}
  public MediaSnapshot Read(){if(manager==null)manager=Result(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());var session=manager.GetCurrentSession();if(session==null){previousKey="";previous=new MediaSnapshot();return previous;}var data=Result(session.TryGetMediaPropertiesAsync());var playback=session.GetPlaybackInfo();var timeline=session.GetTimelineProperties();bool playing=playback!=null&&playback.PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
   var item=new MediaSnapshot{Available=true,Title=string.IsNullOrWhiteSpace(data.Title)?"Contenido multimedia":data.Title,Artist=string.IsNullOrWhiteSpace(data.Artist)?(string.IsNullOrWhiteSpace(data.AlbumTitle)?"Sin artista publicado":data.AlbumTitle):data.Artist,Source=session.SourceAppUserModelId??"WINDOWS MEDIA",Status=playing?"REPRODUCIENDO":"EN PAUSA",Playing=playing};
   if(timeline!=null){item.Duration=Math.Max(0,(timeline.EndTime-timeline.StartTime).TotalSeconds);item.Position=Math.Max(0,(timeline.Position-timeline.StartTime).TotalSeconds);if(playing&&timeline.LastUpdatedTime!=DateTimeOffset.MinValue)item.Position+=Math.Max(0,(DateTimeOffset.UtcNow-timeline.LastUpdatedTime).TotalSeconds);if(item.Duration>0)item.Position=Math.Min(item.Duration,item.Position);}
   item.ThumbnailProvided=data.Thumbnail!=null;string key=item.Source+"|"+item.Title+"|"+item.Artist;item.Cover=key==previousKey?previous.Cover:null;
   if(item.Cover==null&&data.Thumbnail!=null){try{using(var stream=Result(data.Thumbnail.OpenReadAsync())){if(stream.Size>0&&stream.Size<=4*1024*1024){var buffer=Result(stream.ReadAsync(new Windows.Storage.Streams.Buffer((uint)stream.Size),(uint)stream.Size,InputStreamOptions.None));using(var reader=DataReader.FromBuffer(buffer)){var bytes=new byte[buffer.Length];reader.ReadBytes(bytes);using(var memory=new MemoryStream(bytes)){var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=160;bitmap.StreamSource=memory;bitmap.EndInit();bitmap.Freeze();item.Cover=bitmap;}}}}}catch(Exception ex){item.ArtError=ex.GetType().Name+": "+ex.Message;}}
   previousKey=key;previous=item;return item;
  }
  public void Control(string command){Task.Run(()=>{try{if(manager==null)manager=Result(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());var session=manager.GetCurrentSession();if(session==null)return;if(command=="next")Result(session.TrySkipNextAsync());else if(command=="previous")Result(session.TrySkipPreviousAsync());else Result(session.TryTogglePlayPauseAsync());}catch{}});}
  public static string Time(double seconds){var t=TimeSpan.FromSeconds(Math.Max(0,Math.Min(359999,seconds)));return t.TotalHours>=1?((int)t.TotalHours)+":"+t.Minutes.ToString("00")+":"+t.Seconds.ToString("00"):((int)t.TotalMinutes)+":"+t.Seconds.ToString("00");}
 }
 // Reads the default speaker's instantaneous peak. No audio is recorded or saved.
 sealed class SpeakerMeter:IDisposable {
  [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Enumerator {
   [PreserveSig]int EnumAudioEndpoints(int flow,uint mask,out IntPtr devices);
   [PreserveSig]int GetDefaultAudioEndpoint(int flow,int role,out Device device);
  }
  [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Device {
   [PreserveSig]int Activate(ref Guid iid,uint context,IntPtr parameters,[MarshalAs(UnmanagedType.IUnknown)]out object result);
  }
  [ComImport,Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Meter {
   [PreserveSig]int GetPeakValue(out float peak);
  }
  Meter meter;Device device;Enumerator enumerator;Stopwatch reconnect=Stopwatch.StartNew();
  public double Read(){try{if(meter==null){if(reconnect.ElapsedMilliseconds<1500)return 0;reconnect.Restart();enumerator=(Enumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")));Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(0,1,out device));object obj;var iid=new Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064");Marshal.ThrowExceptionForHR(device.Activate(ref iid,23,IntPtr.Zero,out obj));meter=(Meter)obj;}float peak;Marshal.ThrowExceptionForHR(meter.GetPeakValue(out peak));if(reconnect.ElapsedMilliseconds>5000)Dispose();return Math.Max(0,Math.Min(1,peak));}catch{Dispose();reconnect.Restart();return 0;}}
  public void Dispose(){if(meter!=null){Marshal.ReleaseComObject(meter);meter=null;}if(device!=null){Marshal.ReleaseComObject(device);device=null;}if(enumerator!=null){Marshal.ReleaseComObject(enumerator);enumerator=null;}}
 }
}
