using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using Forms=System.Windows.Forms;
using System.Reflection;

[assembly: AssemblyTitle("GridNest")]
[assembly: AssemblyProduct("GridNest")]
[assembly: AssemblyDescription("Desktop folder panels with a snap-to-grid layout")]
[assembly: AssemblyVersion("0.15.3.0")]
[assembly: AssemblyFileVersion("0.15.3.0")]

namespace GridNest {
 public sealed class SettingsLayout {
  public readonly StackPanel Body;
  public readonly StackPanel Actions;
  Grid root;
  ScrollViewer contentScroll;
  TabControl pages;
  public SettingsLayout(Window window) {
   window.MinWidth=Math.Min(window.Width,400);
   window.MinHeight=Math.Min(window.Height,260);
   window.MaxHeight=Program.Area(window.Left,window.Top).Height;
   var grid=new Grid();root=grid;
   grid.RowDefinitions.Add(new RowDefinition());
   grid.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
   Body=new StackPanel {Margin=new Thickness(22,20,22,8)};
   var scroll=new ScrollViewer {Name="SettingsScroll",Content=Body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,PanningMode=PanningMode.VerticalOnly};
   grid.Children.Add(scroll);contentScroll=scroll;
   Actions=new StackPanel {Margin=new Thickness(22,14,22,16)};
   var footer=new Border {BorderThickness=new Thickness(0,1,0,0),Child=Actions};
   footer.SetResourceReference(Border.BorderBrushProperty,"SettingsLine");
   footer.SetResourceReference(Border.BackgroundProperty,"SettingsSurface");
   Grid.SetRow(footer,1);grid.Children.Add(footer);window.Content=grid;
  }
  public StackPanel Page(string title) {
   if(pages==null) {
    pages=new TabControl {Name="SettingsTabs",Margin=new Thickness(20,16,20,8)};
    root.Children.Remove(contentScroll);root.Children.Insert(0,pages);
   }
   var content=new StackPanel {Margin=new Thickness(0,14,8,8)};
   var scroll=new ScrollViewer {Name="SettingsScroll",Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,PanningMode=PanningMode.VerticalOnly};
   pages.Items.Add(new TabItem {Header=title,Content=scroll});
   return content;
  }
  public static StackPanel Section(StackPanel parent,string title) {
   var content=new StackPanel();
   var box=new StackPanel();
   box.Children.Add(new TextBlock {Text=title,FontSize=14,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,8)});
   box.Children.Add(content);
   var border=new Border {Child=box,Padding=new Thickness(12),CornerRadius=new CornerRadius(8),BorderThickness=new Thickness(1),Margin=new Thickness(0,0,0,10)};
   border.SetResourceReference(Border.BackgroundProperty,"SettingsSurface");
   border.SetResourceReference(Border.BorderBrushProperty,"SettingsLine");
   parent.Children.Add(border);return content;
  }
  public static void Buttons(StackPanel actions,Window window,Button primary) {
   var row=new Grid();
   row.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
   row.ColumnDefinitions.Add(new ColumnDefinition());
   row.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
   var cancel=new Button {Name="CancelSettings",Content="Отмена",MinWidth=88,IsCancel=true};
   cancel.Click+=(s,e)=>window.Close();row.Children.Add(cancel);
   primary.MinWidth=144;primary.Margin=new Thickness(12,0,0,0);primary.IsDefault=true;
   Grid.SetColumn(primary,2);row.Children.Add(primary);actions.Children.Add(row);
  }
  public static void Hint(StackPanel parent,string text) {
   var hint=new TextBlock {Text=text,TextWrapping=TextWrapping.Wrap,FontSize=12,Margin=new Thickness(0,4,0,8)};
   hint.SetResourceReference(TextBlock.ForegroundProperty,"UiMuted");parent.Children.Add(hint);
  }
 }

 public static class AppUpdater {
  const string Releases="https://github.com/AdreanHoyk/GridNest/releases";
  const string LatestApi="https://api.github.com/repos/AdreanHoyk/GridNest/releases/latest";
  public static string InstalledVersion {get{return typeof(Program).Assembly.GetName().Version.ToString(3);}}
  [System.Runtime.Serialization.DataContract]
  public sealed class ReleaseInfo {
   [System.Runtime.Serialization.DataMember(Name="tag_name")]public string Tag;
   [System.Runtime.Serialization.DataMember(Name="body")]public string Notes;
   [System.Runtime.Serialization.DataMember(Name="draft")]public bool Draft;
   [System.Runtime.Serialization.DataMember(Name="prerelease")]public bool Prerelease;
   [System.Runtime.Serialization.DataMember(Name="assets")]public ReleaseAsset[] Assets;
   public Version Version;
  }
  [System.Runtime.Serialization.DataContract]
  public sealed class ReleaseAsset {
   [System.Runtime.Serialization.DataMember(Name="name")]public string Name;
   [System.Runtime.Serialization.DataMember(Name="browser_download_url")]public string Url;
   [System.Runtime.Serialization.DataMember(Name="digest")]public string Digest;
   [System.Runtime.Serialization.DataMember(Name="size")]public long Size;
  }
  public static ReleaseInfo ParseRelease(string json) {
   var serializer=new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(ReleaseInfo));
   ReleaseInfo release;
   using(var stream=new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))release=(ReleaseInfo)serializer.ReadObject(stream);
   if(release==null||release.Draft||release.Prerelease||
    !System.Text.RegularExpressions.Regex.IsMatch(release.Tag??"","^v?[0-9]+[.][0-9]+[.][0-9]+$")||
    !Version.TryParse(release.Tag.TrimStart('v'),out release.Version))
    throw new InvalidDataException("Не удалось определить версию обновления.");
   return release;
  }
  public static ReleaseAsset Installer(ReleaseInfo release) {
   return (release.Assets??new ReleaseAsset[0]).FirstOrDefault(asset=>
    asset.Name=="GridNest-Setup.exe"&&asset.Size>0&&
    asset.Url==Releases+"/download/"+Uri.EscapeDataString(release.Tag)+"/GridNest-Setup.exe"&&
    System.Text.RegularExpressions.Regex.IsMatch(asset.Digest??"","^sha256:[0-9a-fA-F]{64}$"));
  }
  public static void VerifyInstaller(string path,ReleaseAsset asset) {
   if(new FileInfo(path).Length!=asset.Size)throw new InvalidDataException("Файл загрузился не полностью. Повторите обновление.");
   string hash;
   using(var algorithm=System.Security.Cryptography.SHA256.Create())
   using(var stream=File.OpenRead(path))hash=BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-","");
   if(!hash.Equals(asset.Digest.Substring(7),StringComparison.OrdinalIgnoreCase))
    throw new InvalidDataException("Контрольная сумма не совпала. Повторите обновление.");
  }
  static string PlainNotes(string value) {
   string text=System.Text.RegularExpressions.Regex.Replace(value??"","(?m)^#{1,6} *","");
   return text.Replace("**","").Replace(((char)96).ToString(),"");
  }
  public static void Show(Window owner=null) {
   if(owner==null)owner=Program.Panels.FirstOrDefault(panel=>panel.IsActive)??Program.Panels.FirstOrDefault(panel=>panel.IsVisible);
   var w=new Window {Title="Обновление GridNest",Width=520,Height=560,Owner=owner,ShowInTaskbar=false,
    WindowStartupLocation=owner==null?WindowStartupLocation.CenterScreen:WindowStartupLocation.CenterOwner};
   var layout=new SettingsLayout(w);
   var status=new TextBlock {Name="UpdateStatus",Text="Проверяю обновления…",TextWrapping=TextWrapping.Wrap,FontSize=18,Margin=new Thickness(0,0,0,12)};
   layout.Body.Children.Add(status);
   SettingsLayout.Hint(layout.Body,"Установлена версия "+InstalledVersion);
   var notes=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,16)};
   layout.Body.Children.Add(notes);
   var releasePage=new Button {Content="Открыть релизы на GitHub ↗",HorizontalAlignment=HorizontalAlignment.Left};
   layout.Body.Children.Add(releasePage);
   releasePage.Click+=(s,e)=>PanelWindow.OpenPath(Releases);
   var check=new Button {Name="CheckUpdates",Content="Проверить ещё раз",Margin=new Thickness(0,0,0,12)};
   layout.Actions.Children.Add(check);
   var install=new Button {Name="InstallUpdate",Content="Скачать и обновить",IsEnabled=false};
   SettingsLayout.Buttons(layout.Actions,w,install);
   bool closed=false,busy=false;
   ReleaseAsset asset=null;
   var client=new System.Net.WebClient();
   client.Encoding=System.Text.Encoding.UTF8;client.Headers["User-Agent"]="GridNest/"+InstalledVersion;
   System.Net.ServicePointManager.SecurityProtocol|=System.Net.SecurityProtocolType.Tls12;
   Func<System.Threading.Tasks.Task> refresh=async ()=> {
    if(busy||closed)return;
    busy=true;check.IsEnabled=false;install.IsEnabled=false;asset=null;notes.Text="";
    status.Text="Проверяю обновления…";
    try {
     client.Headers["Accept"]="application/vnd.github+json";
     string json=await client.DownloadStringTaskAsync(new Uri(LatestApi));
     if(closed)return;
     var release=ParseRelease(json);
     int comparison=release.Version.CompareTo(new Version(InstalledVersion));
     if(comparison<=0) {
      status.Text=comparison==0?"Установлена актуальная версия":"Установленная версия новее опубликованной";
      notes.Text="Последний релиз на GitHub — "+release.Version.ToString(3);return;
     }
     status.Text="Доступна версия "+release.Version.ToString(3);
     notes.Text=PlainNotes(release.Notes);
     asset=Installer(release);
     install.IsEnabled=asset!=null;
     if(asset==null)status.Text+=". Установщик с контрольной суммой пока недоступен.";
    } catch(Exception ex) {
     Debug.WriteLine(ex);
     if(!closed)status.Text="Не удалось проверить обновления. Проверьте подключение и повторите попытку.";
    } finally {busy=false;if(!closed)check.IsEnabled=true;}
   };
   client.DownloadProgressChanged+=(s,e)=> {if(!closed&&asset!=null&&busy)status.Text="Скачиваю обновление · "+e.ProgressPercentage+"%";};
   check.Click+=async (s,e)=>await refresh();
   w.Loaded+=async (s,e)=>await refresh();
   install.Click+=async (s,e)=> {
    if(asset==null||busy)return;
    busy=true;check.IsEnabled=false;install.IsEnabled=false;
    string directory=Path.Combine(Path.GetTempPath(),"GridNest-update-"+Guid.NewGuid().ToString("N"));
    string file=Path.Combine(directory,"GridNest-Setup.exe");
    bool launched=false;
    try {
     Directory.CreateDirectory(directory);client.Headers["Accept"]="application/octet-stream";
     status.Text="Скачиваю обновление…";
     await client.DownloadFileTaskAsync(new Uri(asset.Url),file);
     if(closed)return;
     status.Text="Проверяю файл…";VerifyInstaller(file,asset);
     Process.Start(new ProcessStartInfo(file) {UseShellExecute=true});
     launched=true;w.Close();
    } catch(Exception ex) {
     Debug.WriteLine(ex);
     if(!closed)status.Text=ex is InvalidDataException?ex.Message:"Не удалось скачать или открыть установщик. Повторите попытку или откройте релиз на GitHub.";
    } finally {
     if(!launched)try {if(File.Exists(file))File.Delete(file);if(Directory.Exists(directory))Directory.Delete(directory);}catch(Exception ex) {Debug.WriteLine(ex.Message);}
     busy=false;if(!closed) {check.IsEnabled=true;install.IsEnabled=asset!=null;}
    }
   };
   w.Closed+=(s,e)=> {closed=true;client.CancelAsync();client.Dispose();};
   w.ShowDialog();
  }
 }

 public class Program {
  public static string Home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GridNest", ".app");
  public static string Data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GridNest");
  public static List<PanelWindow> Panels = new List<PanelWindow>();
  static System.Threading.Mutex mutex;
  static Forms.NotifyIcon tray;
  static System.Threading.EventWaitHandle showEvent;
  static System.Threading.RegisteredWaitHandle showWait;
  static System.Threading.EventWaitHandle stopEvent;static System.Threading.RegisteredWaitHandle stopWait;
  static Window searchWindow;
  public const int PanelGrid=16;
  public static int MinPanelGap=16;
  public static int MinVerticalGap=8;
  public static bool Locked;
  static Action undo;
  public static void Remember(Action action) { undo=action; }
  public static void Undo() {
   if(undo==null)return;
   try { undo(); undo=null; } catch(Exception ex) { MessageBox.Show(ex.Message,"Отмена действия"); }
  }
  public static Rect Area(double x,double y) {
   var display=PresentationSource.CurrentSources.OfType<HwndSource>().FirstOrDefault(v=>v.CompositionTarget!=null);
   var transform=display==null?Matrix.Identity:display.CompositionTarget.TransformToDevice;
   var pixel=transform.Transform(new Point(x,y));
   var bounds=Forms.Screen.FromPoint(new System.Drawing.Point((int)pixel.X,(int)pixel.Y)).WorkingArea;
   return new Rect(bounds.X/transform.M11,bounds.Y/transform.M22,bounds.Width/transform.M11,bounds.Height/transform.M22);
  }
  static void SavePreferences() {
   new XElement("Application",new XAttribute("Theme",ThemeMode),new XAttribute("Gap",MinPanelGap),new XAttribute("VerticalGap",MinVerticalGap),new XAttribute("Locked",Locked)).Save(Path.Combine(Home,"app-settings.xml"));
  }
  public static bool LayoutReady;
  public static string ThemeMode="Dark";
  static ResourceDictionary appearance;
  [STAThread] public static void Main(string[] args) {
   int dataArg=Array.IndexOf(args,"--data-root");
   if(dataArg>=0) { if(dataArg+1>=args.Length)throw new ArgumentException("Missing data directory."); Data=Path.GetFullPath(args[dataArg+1]); Home=Path.Combine(Data,".app"); }
   bool first; mutex = new System.Threading.Mutex(true, "Local\\GridNestDesktop", out first);
   if (!first) { try { using(var ev=System.Threading.EventWaitHandle.OpenExisting("Local\\GridNestShow"))ev.Set(); } catch {} return; }
   Directory.CreateDirectory(Home); Directory.CreateDirectory(Data);
   AppDomain.CurrentDomain.UnhandledException += (s,e) => { try { File.WriteAllText(Path.Combine(Home,"error.log"),e.ExceptionObject.ToString()); } catch {} };
   Application app = new Application(); app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
   try { var prefs=XElement.Load(Path.Combine(Home,"app-settings.xml")); int gap=(int?)prefs.Attribute("Gap")??16; MinPanelGap=new[]{-1,0,8,16}.Contains(gap)?gap:16; int vertical=(int?)prefs.Attribute("VerticalGap")??8;MinVerticalGap=new[]{0,8,16}.Contains(vertical)?vertical:8; Locked=(bool?)prefs.Attribute("Locked")??false;string mode=(string)prefs.Attribute("Theme"); if(new[]{"Dark","Light","System"}.Contains(mode))ThemeMode=mode; } catch {}
   try {SavePreferences();}catch(Exception ex) {Debug.WriteLine(ex.Message);}
   ApplyAppearance();
   EventManager.RegisterClassHandler(typeof(Window),FrameworkElement.LoadedEvent,new RoutedEventHandler((s,e)=> {var window=s as Window;if(window!=null&&e.OriginalSource==window)PaintCaption(window);}));
   Microsoft.Win32.SystemEvents.UserPreferenceChanged+=(s,e)=> { if(ThemeMode=="System")app.Dispatcher.BeginInvoke(new Action(ApplyAppearance)); };
   showEvent=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.AutoReset,"Local\\GridNestShow");
   showWait=System.Threading.ThreadPool.RegisterWaitForSingleObject(showEvent,(s,t)=>app.Dispatcher.BeginInvoke(new Action(ShowPanels)),null,-1,false);
   stopEvent=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.AutoReset,"Local\\GridNestStop");stopWait=System.Threading.ThreadPool.RegisterWaitForSingleObject(stopEvent,(s,t)=>app.Dispatcher.BeginInvoke(new Action(()=>app.Shutdown())),null,-1,false);
   string[] preferred={"Работа","AI","Сеть","Разработка","3D и CAD","Медиа","Игры","VR","Браузеры","Система"};
   string[] names=Directory.GetDirectories(Data).Select(Path.GetFileName).Where(n=>!n.StartsWith(".")).OrderBy(n=> {int index=Array.FindIndex(preferred,p=>p.Equals(n,StringComparison.OrdinalIgnoreCase));return index<0?preferred.Length:index;}).ThenBy(n=>n,StringComparer.CurrentCultureIgnoreCase).ToArray();
   if(names.Length==0&&!File.Exists(Path.Combine(Home,"initialized")))names=new[]{"игры"};
   for(int i=0;i<names.Length;i++) { PanelWindow p=new PanelWindow(names[i],i); Panels.Add(p); p.Show(); }
   app.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=> {LayoutReady=true;foreach(var p in Panels) {Point point;if(FindPlacement(p,p.Left,p.Top,p.Width,p.Height,true,out point)) {p.Left=point.X;p.Top=point.Y;p.Save();}}}));
   if(!File.Exists(Path.Combine(Home,"initialized")))File.WriteAllText(Path.Combine(Home,"initialized"),"1");
   if(args.Contains("--arrange"))ArrangePanels();
   SetupTray();
   app.Exit += (s,e) => { foreach(var p in Panels) p.Save(); if(tray!=null) { tray.Visible=false; tray.Dispose(); } if(showWait!=null)showWait.Unregister(null); if(showEvent!=null)showEvent.Dispose();if(stopWait!=null)stopWait.Unregister(null);if(stopEvent!=null)stopEvent.Dispose(); };
   if(args.Contains("--self-test")) {
    var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(3) };
    timer.Tick+=(s,e)=> { timer.Stop(); string report=""; foreach(var p in Panels.ToArray()) report+=p.Test()+Environment.NewLine+p.Diagnostics()+Environment.NewLine; report+=TestFeatures(); File.WriteAllText(Path.Combine(Home,"self-test.txt"),report); app.Shutdown(); }; timer.Start();
   }
   app.Run();
  }
  static void SetupTray() {
   tray=new Forms.NotifyIcon { Text="GridNest", Visible=false };
   using(var stream=typeof(Program).Assembly.GetManifestResourceStream("GridNest.AppIcon.ico")) { using(var icon=new System.Drawing.Icon(stream))tray.Icon=(System.Drawing.Icon)icon.Clone(); }
   var menu=new Forms.ContextMenuStrip();
   Action<string,Action> add=(label,action)=> { var item=new Forms.ToolStripMenuItem(label); item.Click+=(s,e)=>Application.Current.Dispatcher.BeginInvoke(action); menu.Items.Add(item); };
   add("Показать все папки",ShowPanels); add("Скрыть все папки",HidePanels);
   menu.Items.Add(new Forms.ToolStripSeparator());
   add("Поиск приложения…",Search); add("Создать папку…",CreateFolder); add("Выровнять все папки",()=>ArrangePanels());
   var pin=new Forms.ToolStripMenuItem("Закрепить папки") {Checked=Locked,CheckOnClick=true};
   pin.Click+=(s,e)=> { Locked=pin.Checked;SavePreferences();foreach(var panel in Panels)panel.UpdateHandles(); };
   menu.Opening+=(s,e)=>pin.Checked=Locked;
   menu.Items.Add(pin);add("Отменить последнее действие",Undo);
   var folders=new Forms.ToolStripMenuItem("Папки");
   folders.DropDownOpening+=(s,e)=> { folders.DropDownItems.Clear(); foreach(var p in Panels) { var entry=new Forms.ToolStripMenuItem(p.FolderName) { Checked=p.IsVisible }; entry.Click+=(x,y)=>Application.Current.Dispatcher.BeginInvoke(new Action(()=> { if(p.IsVisible)p.Hide();else { p.Show();p.Activate(); } })); folders.DropDownItems.Add(entry); } PaintTrayItems(folders.DropDownItems); }; menu.Items.Add(folders);
   add("Настройки приложения…",AppSettings);add("Проверить обновления…",()=>AppUpdater.Show());
   add("Открыть архив папок",OpenArchive);
   menu.Items.Add(new Forms.ToolStripSeparator()); add("Выход",()=>Application.Current.Shutdown());
   tray.ContextMenuStrip=menu; PaintTray(); tray.DoubleClick+=(s,e)=>Application.Current.Dispatcher.BeginInvoke(new Action(ShowPanels)); tray.Visible=true;
  }
  static bool LightTheme {
   get { if(ThemeMode!="System")return ThemeMode=="Light"; try { return Convert.ToInt32(Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1))!=0; } catch { return false; } }
  }
  public static void ApplyAppearance() {
   string xaml; using(var stream=typeof(Program).Assembly.GetManifestResourceStream("GridNest.Theme.xaml"))using(var reader=new StreamReader(stream))xaml=reader.ReadToEnd();
   if(LightTheme) {
    string[,] colors={{"#141E2A","#F3F7FB"},{"#EDF3F8","#182B3B"},{"#213142","#FFFFFF"},{"#F1F6FA","#182B3B"},{"#8AE5D5","#19756C"},{"#405469","#BDCDDA"},{"#2B4051","#DCE8F1"},{"#F0F5FA","#172F41"},{"#E9F2F7","#243C4F"},{"#1C2A38","#FFFFFF"},{"#354B5C","#C5D5E1"},{"#3B5265","#B8CDD9"},{"#163032","#E4F2EF"},{"#182635","#F8FBFE"},{"#EEF5FA","#1C3447"},{"#405367","#BDCEDB"},{"#31495B","#D7E8F2"},{"#182A3C","#E3EDF4"},{"#638296","#7294A7"},{"#9BE4DA","#19756C"},{"#99B3C6","#526E82"}};
    for(int i=0;i<colors.GetLength(0);i++)xaml=xaml.Replace(colors[i,0],colors[i,1]);
   }
   var next=(ResourceDictionary)System.Windows.Markup.XamlReader.Parse(xaml);
   if(appearance!=null)Application.Current.Resources.MergedDictionaries.Remove(appearance);
   Application.Current.Resources.MergedDictionaries.Add(next); appearance=next; PaintTray();foreach(Window window in Application.Current.Windows)PaintCaption(window);
  }
  [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
  static void PaintCaption(Window window) {try {var handle=new WindowInteropHelper(window).Handle;if(handle!=IntPtr.Zero) {int dark=LightTheme?0:1;DwmSetWindowAttribute(handle,20,ref dark,4);}}catch{} }
  static void PaintTrayItems(Forms.ToolStripItemCollection items) { foreach(Forms.ToolStripItem item in items) { item.ForeColor=LightTheme?System.Drawing.Color.FromArgb(24,43,59):System.Drawing.Color.FromArgb(237,243,248); } }
  static void PaintTray() { if(tray==null||tray.ContextMenuStrip==null)return; tray.ContextMenuStrip.Renderer=new Forms.ToolStripProfessionalRenderer(new TrayColors(LightTheme)); PaintTrayItems(tray.ContextMenuStrip.Items); }
  sealed class TrayColors : Forms.ProfessionalColorTable {
   bool light; public TrayColors(bool value){light=value;UseSystemColors=false;}
   System.Drawing.Color Surface {get{return light?System.Drawing.Color.FromArgb(248,251,254):System.Drawing.Color.FromArgb(24,38,53);}}
   public override System.Drawing.Color ToolStripDropDownBackground {get{return Surface;}}
   public override System.Drawing.Color ImageMarginGradientBegin {get{return Surface;}}
   public override System.Drawing.Color ImageMarginGradientMiddle {get{return Surface;}}
   public override System.Drawing.Color ImageMarginGradientEnd {get{return Surface;}}
   public override System.Drawing.Color MenuBorder {get{return light?System.Drawing.Color.FromArgb(189,206,219):System.Drawing.Color.FromArgb(64,83,103);}}
   public override System.Drawing.Color MenuItemSelected {get{return light?System.Drawing.Color.FromArgb(215,232,242):System.Drawing.Color.FromArgb(49,73,91);}}
   public override System.Drawing.Color MenuItemBorder {get{return MenuItemSelected;}}
  }
  public static void AppSettings() {
   var w=new Window {Title="Настройки приложения — GridNest",Width=460,Height=500,WindowStartupLocation=WindowStartupLocation.CenterScreen};
   var layout=new SettingsLayout(w);
   var appearance=SettingsLayout.Section(layout.Body,"Тема приложения");
   var dark=new RadioButton {Content="Тёмная",GroupName="AppTheme",IsChecked=ThemeMode=="Dark"};
   var light=new RadioButton {Content="Светлая",GroupName="AppTheme",IsChecked=ThemeMode=="Light"};
   var system=new RadioButton {Content="Как в Windows",GroupName="AppTheme",IsChecked=ThemeMode=="System"};
   appearance.Children.Add(dark);appearance.Children.Add(light);appearance.Children.Add(system);
   SettingsLayout.Hint(appearance,"Тема меню и окон настроек. Оформление папок настраивается отдельно.");
   var placement=SettingsLayout.Section(layout.Body,"Размещение папок");
   placement.Children.Add(new TextBlock {Text="Отступ по горизонтали",Margin=new Thickness(0,0,0,6)});
   var gaps=new[]{-1,0,8,16};
   var spacing=new ComboBox {ItemsSource=new[]{"Разрешить наложение","Вплотную","8 пикселей","16 пикселей"},SelectedIndex=Array.IndexOf(gaps,MinPanelGap)};
   placement.Children.Add(spacing);
   placement.Children.Add(new TextBlock {Text="Отступ под названием до следующей папки",Margin=new Thickness(0,10,0,6)});
   var verticalGaps=new[]{0,8,16};
   var verticalSpacing=new ComboBox {ItemsSource=new[]{"Вплотную","8 пикселей","16 пикселей"},SelectedIndex=Array.IndexOf(verticalGaps,MinVerticalGap)};
   placement.Children.Add(verticalSpacing);
   var locked=new CheckBox {Content="Закрепить положение и размер папок",IsChecked=Locked,Margin=new Thickness(0,12,0,0)};
   placement.Children.Add(locked);
   var updates=new Button {Content="Проверить обновления… · "+AppUpdater.InstalledVersion,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,6,0,0)};layout.Body.Children.Add(updates);updates.Click+=(s,e)=>AppUpdater.Show(w);
   var apply=new Button {Name="SaveApplicationSettings",Content="Применить"};
   SettingsLayout.Buttons(layout.Actions,w,apply);
   apply.Click+=(s,e)=> {
    Locked=locked.IsChecked==true;MinPanelGap=gaps[spacing.SelectedIndex];MinVerticalGap=verticalGaps[verticalSpacing.SelectedIndex];
    SelectTheme(light.IsChecked==true?"Light":system.IsChecked==true?"System":"Dark");
    foreach(var panel in Panels) {panel.RefreshIconTheme();panel.UpdateHandles();}
    w.Close();
   };
   w.ShowDialog();
  }

  public static void SelectTheme(string mode) { if(!new[]{"Dark","Light","System"}.Contains(mode))throw new ArgumentException("Unknown theme");ThemeMode=mode;SavePreferences();ApplyAppearance(); }
  public static void ShowPanels() { foreach(var p in Panels) { p.Show(); p.WindowState=WindowState.Normal; } if(Panels.Count>0)Panels[0].Activate(); }
  public static bool Fits(PanelWindow except,double x,double y,double width,double height) {
   var a=Area(x,y);if(x<a.Left-.01||y<a.Top-.01||x+width>a.Right+.01||y+height>a.Bottom+.01)return false;
   return ClearOfPanels(except,x,y,width,height);
  }
  static bool ClearOfPanels(PanelWindow except,double x,double y,double width,double height) {
   return MinPanelGap<0 || Panels.Where(p=>p!=except).All(p=>x+width<=p.Left-MinPanelGap+.01||x>=p.Left+p.Width+MinPanelGap-.01||y+height<=p.Top-MinVerticalGap+.01||y>=p.Top+p.Height+MinVerticalGap-.01);
  }
  public static bool FindPlacement(PanelWindow panel,double x,double y,double width,double height,bool search,out Point result) {
   var a=Area(x,y);double maxX=a.Left+Math.Floor((a.Width-width)/PanelGrid)*PanelGrid,maxY=a.Top+Math.Floor((a.Height-height)/PanelGrid)*PanelGrid;
   x=Math.Max(a.Left,Math.Min(maxX,a.Left+Math.Round((x-a.Left)/PanelGrid)*PanelGrid));y=Math.Max(a.Top,Math.Min(maxY,a.Top+Math.Round((y-a.Top)/PanelGrid)*PanelGrid));result=new Point(x,y);
   if(width>a.Width||height>a.Height)return false;
   if(ClearOfPanels(panel,x,y,width,height))return true;if(!search)return false;
   double nearest=double.MaxValue;bool found=false;
   for(double cy=a.Top;cy<=maxY;cy+=PanelGrid)for(double cx=a.Left;cx<=maxX;cx+=PanelGrid) {double distance=(cx-x)*(cx-x)+(cy-y)*(cy-y);if(distance<nearest&&ClearOfPanels(panel,cx,cy,width,height)) {nearest=distance;result=new Point(cx,cy);found=true;}}
   return found;
  }
  public static void HidePanels() { foreach(var p in Panels)p.Hide(); if(searchWindow!=null)searchWindow.Hide(); }
  public static void Prompt(string title,string initial,Action<string> apply) {
   var w=new Window { Title=title, Width=420, Height=220, ResizeMode=ResizeMode.NoResize, WindowStartupLocation=WindowStartupLocation.CenterScreen };
   var layout=new SettingsLayout(w);var sp=layout.Body;
   var input=new TextBox { Text=initial, Margin=new Thickness(0,0,0,14) }; sp.Children.Add(input);
   var ok=new Button { Content="Создать", IsDefault=true, Padding=new Thickness(10,6,10,6) }; SettingsLayout.Buttons(layout.Actions,w,ok);
   ok.Click+=(s,e)=> { try { apply(input.Text); w.Close(); } catch(Exception ex) { MessageBox.Show(w,ex.Message,"Не удалось создать папку"); } };
   w.Loaded+=(s,e)=> { input.Focus(); input.SelectAll(); }; w.ShowDialog();
  }
  public static PanelWindow NewPanel(string name,bool files=true) {
   string target=PanelWindow.ValidateFolderName(name);
   if(Directory.Exists(target)||File.Exists(target))throw new IOException("Папка с таким именем уже существует.");
   Directory.CreateDirectory(target);
   var panel=new PanelWindow(name,Panels.Count);panel.storeFiles=files;if(LayoutReady&&!panel.PrepareGrid()) {panel.Close();if(!Directory.EnumerateFileSystemEntries(target).Any())Directory.Delete(target);throw new IOException("Для новой папки не хватает места. Уменьшите или переместите другие панели.");}Panels.Add(panel);panel.Show();
   var a=SystemParameters.WorkArea; bool placed=false;
   for(double y=a.Top+16;y+panel.Height<=a.Bottom&&!placed;y+=PanelGrid)for(double x=a.Right-panel.Width-16;x>=a.Left;x-=PanelGrid) {
    if(ClearOfPanels(panel,x,y,panel.Width,panel.Height)) { panel.Left=x; panel.Top=y; placed=true; break; }
   }
   panel.AlignPosition(); panel.Save(); return panel;
  }
  public static void CreateFolder() {
   var w=new Window { Title="Новая папка — GridNest", Width=480, Height=350, WindowStartupLocation=WindowStartupLocation.CenterScreen };
   var layout=new SettingsLayout(w);var sp=layout.Body;
   sp.Children.Add(new TextBlock { Text="Название папки", Margin=new Thickness(0,0,0,6) });
   var input=new TextBox { Text="Новая папка", Margin=new Thickness(0,0,0,12) }; sp.Children.Add(input);
   var files=new RadioButton { Content="Файлы — перемещать оригиналы внутрь", IsChecked=true, GroupName="Mode" }; sp.Children.Add(files);
   sp.Children.Add(new RadioButton { Content="Ярлыки — создавать ссылки на файлы", GroupName="Mode" });
   var create=new Button { Content="Создать папку", IsDefault=true, Margin=new Thickness(0,18,0,0) }; SettingsLayout.Buttons(layout.Actions,w,create);
   create.Click+=(s,e)=> { try { NewPanel(input.Text,files.IsChecked==true); w.Close(); } catch(Exception ex) { MessageBox.Show(w,ex.Message,"Создание папки"); } }; w.Loaded+=(s,e)=> { input.Focus();input.SelectAll(); }; w.ShowDialog();
  }
  public static void OpenArchive() { var p=Path.Combine(Data,".archive"); Directory.CreateDirectory(p); PanelWindow.OpenPath(p); }
  public static void ArrangePanels() {
   if(Locked)return;
   if(Panels.Count==0)return;
   var area=SystemParameters.WorkArea; double width=Panels.Max(p=>p.Width), step=Math.Ceiling((width+16)/PanelGrid)*PanelGrid;
   int maxCols=Math.Max(1,(int)Math.Floor((area.Width-32)/step)); int primary=Math.Min(3,maxCols);
   double start=area.Left+Math.Floor((area.Width-16-primary*step)/PanelGrid)*PanelGrid;
   var bottoms=Enumerable.Repeat(area.Top+16,maxCols).ToArray(); int missed=0;
   foreach(var p in Panels) {
    int best=-1; double bestY=double.MaxValue;
    for(int c=0;c<maxCols;c++) { double x=c<primary?start+c*step:start-(c-primary+1)*step; double y=bottoms[c]; if(x>=area.Left&&x+p.Width<=area.Right&&y+p.Height<=area.Bottom&&(best<0||c<primary&&best>=primary||((c<primary)==(best<primary)&&y<bestY))) { best=c; bestY=y; } }
    if(best<0) { missed++; continue; }
    p.Left=best<primary?start+best*step:start-(best-primary+1)*step; p.Top=bestY; p.Save(); bottoms[best]=area.Top+Math.Ceiling((bestY-area.Top+p.Height+MinVerticalGap)/PanelGrid)*PanelGrid;
   }
   if(missed>0)MessageBox.Show("Для всех папок не хватило места. Уменьшите размер крупных папок и повторите выравнивание.","Выравнивание");
  }
  public class SearchEntry {
   public string Path,Group,Label;
   public override string ToString() { return Label+"   —   "+Group; }
  }
  public static List<SearchEntry> Find(string query) {
   string[] words=(query??"").Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries); var results=new List<SearchEntry>();
   foreach(var p in Panels)try { foreach(var file in p.FileEntries()) { string label=Directory.Exists(file)?Path.GetFileName(file):Path.GetFileNameWithoutExtension(file); string hay=label+" "+p.FolderName; if(words.All(w=>hay.IndexOf(w,StringComparison.CurrentCultureIgnoreCase)>=0))results.Add(new SearchEntry { Path=file,Group=p.FolderName,Label=label }); } } catch(IOException) {} catch(UnauthorizedAccessException) {}
   return results.OrderBy(r=>r.Label,StringComparer.CurrentCultureIgnoreCase).ToList();
  }
  public static void Search() {
   if(searchWindow!=null) { searchWindow.Show(); searchWindow.Activate(); return; }
   var w=new Window { Title="Поиск — GridNest", Width=620, Height=460, MinWidth=380, MinHeight=260, WindowStartupLocation=WindowStartupLocation.CenterScreen };
   searchWindow=w; var grid=new Grid { Margin=new Thickness(16) }; grid.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition { Height=new GridLength(1,GridUnitType.Star) }); grid.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); w.Content=grid;
   var input=new TextBox { FontSize=18, Padding=new Thickness(8), ToolTip="Название приложения или папки", Margin=new Thickness(0,0,0,12) }; grid.Children.Add(input);
   var list=new ListBox { FontSize=15 }; Grid.SetRow(list,1); grid.Children.Add(list);
   var footer=new StackPanel { Orientation=Orientation.Horizontal, Margin=new Thickness(0,10,0,0) }; Grid.SetRow(footer,2); grid.Children.Add(footer);
   var open=new Button { Content="Открыть", Padding=new Thickness(12,5,12,5), Margin=new Thickness(0,0,8,0) }; footer.Children.Add(open);
   var locate=new Button { Content="Показать в папке", Padding=new Thickness(12,5,12,5) }; footer.Children.Add(locate);
   Action run=()=> { var item=list.SelectedItem as SearchEntry; if(item!=null)PanelWindow.OpenPath(item.Path); };
   Action update=()=> { list.ItemsSource=Find(input.Text); if(list.Items.Count>0)list.SelectedIndex=0; };
   input.TextChanged+=(s,e)=>update(); open.Click+=(s,e)=>run(); list.MouseDoubleClick+=(s,e)=>run();
   locate.Click+=(s,e)=> { var item=list.SelectedItem as SearchEntry; if(item!=null)Process.Start("explorer.exe","/select,\""+item.Path+"\""); };
   w.PreviewKeyDown+=(s,e)=> { if(e.Key==Key.Enter) { run(); e.Handled=true; } else if(e.Key==Key.Escape)w.Close(); else if(e.Key==Key.Down&&input.IsKeyboardFocused&&list.Items.Count>0) { list.Focus(); e.Handled=true; } };
   w.Activated+=(s,e)=>update(); w.Loaded+=(s,e)=>input.Focus(); w.Closed+=(s,e)=>searchWindow=null; update(); w.Show();
  }
  static string TestFeatures() {
   string name="Feature-test-"+Guid.NewGuid().ToString("N"); PanelWindow p=null; string archive=null;
   string oldTheme=ThemeMode;
   try {
    p=NewPanel(name); File.WriteAllText(Path.Combine(p.FolderPath,"search-marker.txt"),"keep");
    bool create=Panels.Contains(p)&&Directory.Exists(p.FolderPath);
    bool search=Find("search-marker "+name).Count==1;
    p.Left+=7; p.Top+=9; p.AlignPosition(); bool grid=Math.Abs((p.Left-SystemParameters.WorkArea.Left)/PanelGrid-Math.Round((p.Left-SystemParameters.WorkArea.Left)/PanelGrid))<.001&&Math.Abs((p.Top-SystemParameters.WorkArea.Top)/PanelGrid-Math.Round((p.Top-SystemParameters.WorkArea.Top)/PanelGrid))<.001;
    p.Hide(); p.Show(); bool visibility=p.IsVisible;
    archive=p.Archive(); p=null; bool remove=Directory.Exists(archive)&&File.Exists(Path.Combine(archive,"search-marker.txt"))&&!Panels.Any(z=>z.FolderName==name);
    SelectTheme("Light");bool themes=((SolidColorBrush)Application.Current.Resources["UiAccent"]).Color==Color.FromRgb(25,117,108)&&(string)XElement.Load(Path.Combine(Home,"app-settings.xml")).Attribute("Theme")=="Light";SelectTheme("Dark");themes=themes&&((SolidColorBrush)Application.Current.Resources["UiAccent"]).Color==Color.FromRgb(138,229,213);SelectTheme("System");themes=themes&&(string)XElement.Load(Path.Combine(Home,"app-settings.xml")).Attribute("Theme")=="System";
    bool separation=Panels.All(z=>Fits(z,z.Left,z.Top,z.Width,z.Height));if(Panels.Count>1) {var a=Panels[0];var b=Panels[1];Point point;separation=separation&&!FindPlacement(a,b.Left,b.Top,a.Width,a.Height,false,out point)&&FindPlacement(a,b.Left,b.Top,a.Width,a.Height,true,out point)&&Fits(a,point.X,point.Y,a.Width,a.Height)&&a.TestCollisionGuard();}
    return "FEATURES: create="+(create?"PASS":"FAIL")+"; search="+(search?"PASS":"FAIL")+"; panel grid="+(grid?"PASS":"FAIL")+"; show/hide="+(visibility?"PASS":"FAIL")+"; archive without data loss="+(remove?"PASS":"FAIL")+"; tray="+(tray!=null&&tray.Visible?"PASS":"FAIL")+"; tray-only="+(Panels.All(z=>!z.ShowInTaskbar)?"PASS":"FAIL")+"; themes/persist="+(themes?"PASS":"FAIL")+"; separation/move/resize="+(separation?"PASS":"FAIL");
   } catch(Exception e) { return "FEATURES: FAIL "+e; }
   finally { SelectTheme(oldTheme);if(p!=null)archive=p.Archive(); if(archive!=null) { string parent=Path.GetDirectoryName(archive); if(Path.GetFullPath(parent).StartsWith(Path.GetFullPath(Path.Combine(Data,".archive"))+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(archive)==name)Directory.Delete(parent,true); } string config=Path.Combine(Home,name+".xml"); if(File.Exists(config))File.Delete(config); }
  }
 }
 public class PanelWindow : Window {
  string name, folder, config; Border card; WrapPanel icons; ScrollViewer scroll; Grid frame; TextBlock caption; MenuItem animationMenu;
  const string InternalDrag="GridNest.InternalFile";
  int columns=3, rows=3; bool snapping;
  bool darkCaption;
  FrameworkElement moveMarker;
  public void UpdateHandles() {
   foreach(var marker in new[]{moveMarker,resizeHandle}) {
    marker.Visibility=Program.Locked?Visibility.Hidden:Visibility.Visible;
    Fade(marker,IsMouseOver ? 1 : 0,100);
   }
  }
  void PaintLabel() {
   caption.Foreground=darkCaption?Brushes.Black:Brushes.White;
   caption.Effect=new System.Windows.Media.Effects.DropShadowEffect {Color=darkCaption?Colors.White:Colors.Black,BlurRadius=3,ShadowDepth=0,Opacity=1};
  }
  bool retired; bool animations=true; public bool storeFiles=true; FrameworkElement resizeHandle; List<string> order=new List<string>();
  string OrderFile { get { return Path.Combine(folder,".gridnest-order.xml"); } }
  public string FolderName { get { return name; } }
  public string FolderPath { get { return folder; } }
  double CellWidth { get { return iconSize+16; } }
  double CellHeight { get { return iconSize+(labels?36:16); } }
  DispatcherTimer debounce; FileSystemWatcher watcher; bool loaded, labels=false; int iconSize=56; double alpha=0.84;
  Color background=Color.FromRgb(44,46,55);
  Dictionary<string,ImageSource> cache=new Dictionary<string,ImageSource>(StringComparer.OrdinalIgnoreCase);
  public PanelWindow(string n,int index) {
   name=n; folder=Path.Combine(Program.Data,n); config=Path.Combine(Program.Home,n+".xml"); Directory.CreateDirectory(folder);
   Title="GridNest — "+name; Width=266; Height=264; Left=30+index*290; Top=90;
   using(var stream=typeof(Program).Assembly.GetManifestResourceStream("GridNest.AppIcon.ico")) { if(stream!=null)Icon=BitmapFrame.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad); }
   MinWidth=1; MinHeight=1; WindowStyle=WindowStyle.None; AllowsTransparency=true; Background=Brushes.Transparent;
   ShowInTaskbar=false; ResizeMode=ResizeMode.NoResize;
   Load();LoadDefaultStyle();
   frame=new Grid(); frame.RowDefinitions.Add(new RowDefinition()); frame.RowDefinitions.Add(new RowDefinition { Height=new GridLength(38) }); Content=frame;
   card=new Border { CornerRadius=new CornerRadius(30), Padding=new Thickness(0), AllowDrop=true }; UpdateBackground();
   frame.Children.Add(card);
   glowLayer=new Border {CornerRadius=new CornerRadius(30),IsHitTestVisible=false};
   outlineLayer=new Border {CornerRadius=new CornerRadius(30),IsHitTestVisible=false};
   var glowHost=new Grid {IsHitTestVisible=false};
   glowHost.Children.Add(glowLayer);
   glowHost.SizeChanged+=(s,e)=>glowHost.Clip=new RectangleGeometry(new Rect(0,0,glowHost.ActualWidth,glowHost.ActualHeight),30,30);
   frame.Children.Add(glowHost);frame.Children.Add(outlineLayer);UpdateOutline();
   Grid content=new Grid(); card.Child=content;

   icons=new WrapPanel { Orientation=Orientation.Horizontal, VerticalAlignment=VerticalAlignment.Top };
   scroll=new ScrollViewer { Content=icons, HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled, VerticalScrollBarVisibility=ScrollBarVisibility.Auto, PanningMode=PanningMode.VerticalOnly,Margin=new Thickness(16),ClipToBounds=false };
   scroll.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:p='clr-namespace:System.Windows.Controls.Primitives;assembly=PresentationFramework' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ScrollViewer'><Grid><ScrollContentPresenter x:Name='PART_ScrollContentPresenter' Content='{TemplateBinding Content}' ContentTemplate='{TemplateBinding ContentTemplate}' CanContentScroll='{TemplateBinding CanContentScroll}'/><p:ScrollBar x:Name='PART_VerticalScrollBar' Width='8' Margin='0,0,-8,0' HorizontalAlignment='Right' Orientation='Vertical' Maximum='{TemplateBinding ScrollableHeight}' ViewportSize='{TemplateBinding ViewportHeight}' Value='{Binding VerticalOffset,RelativeSource={RelativeSource TemplatedParent},Mode=OneWay}' Visibility='{TemplateBinding ComputedVerticalScrollBarVisibility}'/></Grid></ControlTemplate>");
   content.Children.Add(scroll);
   caption=new TextBlock { Text=name, FontFamily=new FontFamily("Segoe UI"), FontSize=19, Foreground=Brushes.White, TextAlignment=TextAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center };
   PaintLabel();
   Grid.SetRow(caption,1); frame.Children.Add(caption);
   // An explicit resize handle works with transparent borderless windows.
   System.Windows.Controls.Primitives.Thumb grip=new System.Windows.Controls.Primitives.Thumb { Width=36, Height=16, HorizontalAlignment=HorizontalAlignment.Right, VerticalAlignment=VerticalAlignment.Bottom, Margin=new Thickness(0,0,1,5), Cursor=Cursors.SizeNWSE, Opacity=0, ToolTip="Потяните, чтобы изменить размер папки" };
   resizeHandle=grip;AttachTouchResize(grip);grip.Template=GripTemplate();content.Children.Add(grip);
   var moveDots=new Canvas {Width=12,Height=10,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
   for(int y=0;y<3;y++)for(int x=0;x<2;x++) {var dot=new System.Windows.Shapes.Ellipse {Width=2,Height=2};dot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty,"PanelHandle");Canvas.SetLeft(dot,3+x*4);Canvas.SetTop(dot,1+y*3);moveDots.Children.Add(dot);}
   var moveHandle=new Border {Child=moveDots,Width=48,Height=12,Background=Brushes.Transparent,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,2),Cursor=Cursors.SizeAll,ToolTip="Перетащить папку"};
    content.Children.Add(moveHandle);
   AttachDrag(moveHandle);
   moveMarker=moveHandle;MouseEnter+=(s,e)=>UpdateHandles();MouseLeave+=(s,e)=>UpdateHandles();UpdateHandles();
   Action undoSize=null; double dragW=0,dragH=0; Point dragOrigin=new Point();
   grip.DragStarted+=(s,e)=> { undoSize=CaptureLayout();dragW=Width; dragH=Height; dragOrigin=Mouse.GetPosition(this); };
   grip.DragDelta+=(s,e)=> { Point now=Mouse.GetPosition(this); SnapSize(dragW+now.X-dragOrigin.X,dragH+now.Y-dragOrigin.Y); };
   grip.DragCompleted+=(s,e)=> { AlignPosition(); Save();if(Width!=dragW||Height!=dragH)Program.Remember(undoSize); };
   AttachDrag(card,true);AttachDrag(caption);
   card.PreviewDragOver+=(s,e)=> { e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?(storeFiles||e.Data.GetDataPresent(InternalDrag)||((string[])e.Data.GetData(DataFormats.FileDrop)).All(p=>IsDesktopItem(p)&&IsShortcut(p))?DragDropEffects.Move:DragDropEffects.Copy):DragDropEffects.None; e.Handled=true; };
   card.Drop+=DropFiles;
   ContextMenu menu=new ContextMenu();
   Add(menu,"Открыть папку",()=>Open(folder));
   var search=Add(menu,"Поиск…",Program.Search);search.InputGestureText="Ctrl+F";
   menu.Items.Add(new Separator());
   var folderMenu=new MenuItem {Header="Папка"};menu.Items.Add(folderMenu);
   Add(folderMenu,"Переименовать…",RenameFolder);
   Add(folderMenu,"Создать новую…",Program.CreateFolder);
   Add(folderMenu,"Обновить значки",()=> {cache.Clear();Refresh();});
   folderMenu.Items.Add(new Separator());
   var removeMenu=new MenuItem {Header="Убрать папку"};folderMenu.Items.Add(removeMenu);
   Add(removeMenu,"На рабочий стол…",RemoveToDesktop);
   Add(removeMenu,"В архив…",()=> {
    if(MessageBox.Show("Убрать папку «"+name+"» с рабочего стола?\nСодержимое сохранится в архиве GridNest\\.archive.","Убрать папку",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes) {
     try {Archive();}catch(Exception ex) {MessageBox.Show(ex.Message,"Не удалось убрать папку");}
    }
   });
   var appearanceMenu=new MenuItem {Header="Оформление"};menu.Items.Add(appearanceMenu);
   Add(appearanceMenu,"Настроить папку…",Settings);
   appearanceMenu.Items.Add(new Separator());
   Add(appearanceMenu,"Применить к другим…",()=>ShareStyle());
   Add(appearanceMenu,"Скопировать",()=>copiedStyle=CurrentStyle());
   var paste=Add(appearanceMenu,"Вставить…",()=>ShareStyle(true));
   appearanceMenu.Items.Add(new Separator());
   var names=new MenuItem {Header="Названия значков",IsCheckable=true};appearanceMenu.Items.Add(names);
   names.Click+=(s,e)=> {bool previous=labels;labels=names.IsChecked;if(!SetGrid(columns,rows))labels=previous;names.IsChecked=labels;Refresh();Save();};
   var motion=new MenuItem {Header="Анимации",IsCheckable=true,IsChecked=animations};
   animationMenu=motion;appearanceMenu.Items.Add(motion);
   motion.Click+=(s,e)=> {animations=motion.IsChecked;UpdateOutline();Refresh();Save();};
   var placementMenu=new MenuItem {Header="Размещение"};menu.Items.Add(placementMenu);
   Add(placementMenu,"Выровнять значки в этой папке",AlignIcons);
   placementMenu.Items.Add(new Separator());
   Add(placementMenu,"Выровнять все по сетке",Program.ArrangePanels);
   var top=new MenuItem {Header="Поверх окон",IsCheckable=true};placementMenu.Items.Add(top);
   top.Click+=(s,e)=> {Topmost=top.IsChecked;Save();};
   menu.Items.Add(new Separator());
   Add(menu,"Настройки приложения…",Program.AppSettings);
   Add(menu,"Помощь",()=>Open(Path.Combine(Program.Home,"README.txt")));
   Add(menu,"Выход",()=>Application.Current.Shutdown());
   menu.Opened+=(s,e)=> {names.IsChecked=labels;motion.IsChecked=animations;top.IsChecked=Topmost;paste.IsEnabled=copiedStyle!=null;};
   frame.ContextMenu=menu;
   PreviewMouseWheel+=(s,e)=> { if((Keyboard.Modifiers&ModifierKeys.Control)!=0) { SetIconSize(iconSize+(e.Delta>0?8:-8)); e.Handled=true; } };
   PreviewKeyDown+=(s,e)=> { if(e.Key==Key.Z&&Keyboard.Modifiers==ModifierKeys.Control) {Program.Undo();e.Handled=true;} else if(e.Key==Key.F&&(Keyboard.Modifiers&ModifierKeys.Control)!=0) { Program.Search(); e.Handled=true; } };
   debounce=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(450) }; debounce.Tick+=(s,e)=> { debounce.Stop(); cache.Clear(); Refresh(); };
   watcher=new FileSystemWatcher(folder) { NotifyFilter=NotifyFilters.FileName|NotifyFilters.DirectoryName|NotifyFilters.LastWrite, EnableRaisingEvents=true };
   FileSystemEventHandler changed=(s,e)=>Dispatcher.BeginInvoke(new Action(()=> { debounce.Stop(); debounce.Start(); }));
   watcher.Created+=changed; watcher.Deleted+=changed; watcher.Changed+=changed; watcher.Renamed+=(s,e)=>changed(s,e);
   Loaded+=(s,e)=> { LoadOrder(); SetGrid(columns,rows); loaded=true; Clamp(); AlignPosition(); Refresh();UpdateOutline(); Save(); Reveal(); };
   IsVisibleChanged+=(s,e)=> { if(IsVisible&&loaded)Reveal(); };
   SizeChanged+=(s,e)=> { if(loaded&&!snapping) { SnapSize(Width,Height); Save(); } };
   LocationChanged+=(s,e)=>PositionOuterGlow();SizeChanged+=(s,e)=>PositionOuterGlow();
   IsVisibleChanged+=(s,e)=>UpdateOuterGlow();
   Closed+=(s,e)=> {if(haloWindow!=null)haloWindow.Close();Save();watcher.Dispose();debounce.Stop();};
  }
  static ControlTemplate GripTemplate() {
   return (ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Thumb'><Grid Background='Transparent'><Canvas Width='20' Height='12' HorizontalAlignment='Center' VerticalAlignment='Center' IsHitTestVisible='False'><Path Data='M 3,11 L 15,0 M 9,11 L 16,5' Stroke='#DD101820' StrokeThickness='4' StrokeStartLineCap='Round' StrokeEndLineCap='Round'/><Path x:Name='Glyph' Data='M 3,11 L 15,0 M 9,11 L 16,5' Stroke='White' StrokeThickness='1.8' StrokeStartLineCap='Round' StrokeEndLineCap='Round'/></Canvas></Grid><ControlTemplate.Triggers><Trigger Property='IsDragging' Value='True'><Setter TargetName='Glyph' Property='Stroke' Value='#8AE5D5'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
  }
  static MenuItem Add(ItemsControl m,string text,Action action) { var item=new MenuItem { Header=text }; item.Click+=(s,e)=>action(); m.Items.Add(item);return item; }
  void Load() {
   try { var x=XElement.Load(config); Width=Get(x,"Width",Width); Height=Get(x,"Height",Height); Left=Get(x,"Left",Left); Top=Get(x,"Top",Top); iconSize=(int)Math.Max(28,Math.Min(128,Get(x,"IconSize",64))); alpha=Math.Max(0,Math.Min(1,Get(x,"Opacity",.84))); darkCaption=(bool?)x.Attribute("DarkCaption")??false; labels=(string)x.Attribute("Labels")=="true"; Topmost=(string)x.Attribute("Topmost")=="true"; columns=(int)Get(x,"Columns",Math.Round((Width-32)/CellWidth)); rows=(int)Get(x,"Rows",Math.Round((Height-66)/CellHeight)); } catch {}
   try {var x=XElement.Load(config);iconTheme=ValidIconTheme((string)x.Attribute("IconTheme"));}catch{}
   try { var x=XElement.Load(config); if(x.Attribute("Background")!=null)background=ParseColor((string)x.Attribute("Background")); } catch {}
   try {var x=XElement.Load(config);glowPulse=(bool?)x.Attribute("GlowPulse")??false;glowMode=ValidGlowMode((string)x.Attribute("GlowMode"));outlineColor=ParseColor((string)x.Attribute("OutlineColor")??"#8AE5D5");outlineThickness=Math.Max(0,Math.Min(8,Get(x,"OutlineThickness",0)));outlineGlow=Math.Max(0,Math.Min(100,Get(x,"OutlineGlow",0)));}catch{}
   try { animations=(string)XElement.Load(config).Attribute("Animations")!="false"; } catch {}
   try { storeFiles=(string)XElement.Load(config).Attribute("Mode")!="Shortcuts"; } catch {}
   Width=Math.Max(MinWidth,Width); Height=Math.Max(MinHeight,Height);
  }
  static double Get(XElement e,string key,double d) { double v; return double.TryParse((string)e.Attribute(key),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out v)&&!double.IsNaN(v)&&!double.IsInfinity(v)?v:d; }
  bool SetGrid(int c,int r,bool relocate=true) {
   var a=Program.Area(Left,Top);
   int nextColumns=Math.Max(1,Math.Min(c,Math.Max(1,(int)Math.Floor((a.Width-32)/CellWidth))));
   int nextRows=Math.Max(1,Math.Min(r,Math.Max(1,(int)Math.Floor((a.Height-70)/CellHeight))));
   double width=32+nextColumns*CellWidth,height=70+nextRows*CellHeight;Point position=new Point(Left,Top);
   if(Program.Locked&&loaded&&(nextColumns!=columns||nextRows!=rows||Math.Abs(width-Width)>.01||Math.Abs(height-Height)>.01))return false;
   if(Program.LayoutReady&&!Program.FindPlacement(this,Left,Top,width,height,relocate,out position))return false;
   columns=nextColumns;rows=nextRows;
   snapping=true;
   try { Width=width;Height=height;if(Program.LayoutReady) {Left=position.X;Top=position.Y;} if(icons!=null)icons.Width=columns*CellWidth;
    if(moveMarker!=null) {
     moveMarker.Width=Math.Min(48,Math.Max(12,Width-56));
     moveMarker.HorizontalAlignment=Width<140?HorizontalAlignment.Left:HorizontalAlignment.Center;
     moveMarker.Margin=new Thickness(Width<140?4:0,0,0,2);
    } }
   finally { snapping=false; }
   return true;
  }
  public bool PrepareGrid() {return SetGrid(columns,rows);}
  public bool TestCollisionGuard() {double w=Width,h=Height;return !SetGrid(100,100,false)&&Width==w&&Height==h;}
  void SnapSize(double w,double h) { SetGrid((int)Math.Round((w-32)/CellWidth),(int)Math.Round((h-70)/CellHeight),false); }
  void Clamp() { var a=Program.Area(Left,Top); SetGrid(columns,rows); Left=Math.Max(a.Left,Math.Min(Left,a.Right-Width)); Top=Math.Max(a.Top,Math.Min(Top,a.Bottom-Height)); }
  public void AlignPosition() {
   if(Program.LayoutReady) {Point point;if(Program.FindPlacement(this,Left,Top,Width,Height,true,out point)) {Left=point.X;Top=point.Y;}return;}
   var a=Program.Area(Left,Top); double maxX=Math.Max(0,Math.Floor((a.Width-Width)/Program.PanelGrid)),maxY=Math.Max(0,Math.Floor((a.Height-Height)/Program.PanelGrid));
   Left=a.Left+Math.Max(0,Math.Min(maxX,Math.Round((Left-a.Left)/Program.PanelGrid)))*Program.PanelGrid;
   Top=a.Top+Math.Max(0,Math.Min(maxY,Math.Round((Top-a.Top)/Program.PanelGrid)))*Program.PanelGrid;
  }
  Action CaptureLayout() {
   double x=Left,y=Top;int c=columns,r=rows,size=iconSize;bool names=labels;
   return ()=> {
    if(retired||Program.Locked)throw new IOException("Снимите закрепление и убедитесь, что папка открыта.");
    double currentX=Left,currentY=Top;int currentSize=iconSize;bool currentNames=labels;
    Left=x;Top=y;iconSize=size;labels=names;
    if(!SetGrid(c,r,false)) {Left=currentX;Top=currentY;iconSize=currentSize;labels=currentNames;throw new IOException("Прежнее место занято.");}
    cache.Clear();Refresh();Save();
   };
  }
  void AttachDrag(UIElement surface,bool backgroundOnly=false) {
   if(!backgroundOnly)AttachTouchPanelMove(surface);
   bool dragging=false;Point start=new Point();double left=0,top=0;
   Func<Point> cursor=()=> {var pixel=PointToScreen(Mouse.GetPosition(this));var source=PresentationSource.FromVisual(this);return source!=null?source.CompositionTarget.TransformFromDevice.Transform(pixel):pixel;};
   System.Windows.Controls.Primitives.Popup preview=null;
   Point destination=new Point();Action restore=null;
   Action cancel=()=> {
    if(!dragging)return;
    dragging=false;Left=left;Top=top;if(preview!=null)preview.IsOpen=false;
    if(surface.IsMouseCaptured)surface.ReleaseMouseCapture();
   };
   surface.MouseLeftButtonDown+=(s,e)=> {
    if(TouchMouse(e)||Program.Locked || (backgroundOnly&&e.OriginalSource!=card&&e.OriginalSource!=icons))return;
    Activate();Focus();start=cursor();left=Left;top=Top;restore=CaptureLayout();
    destination=new Point(left,top);dragging=surface.CaptureMouse();e.Handled=dragging;
   };
   surface.MouseMove+=(s,e)=> {
    if(!dragging)return;
    if(e.LeftButton!=MouseButtonState.Pressed) {cancel();return;}
    var now=cursor();var area=Program.Area(now.X,now.Y);
    Left=Math.Max(area.Left,Math.Min(area.Right-Width,left+now.X-start.X));
    Top=Math.Max(area.Top,Math.Min(area.Bottom-Height,top+now.Y-start.Y));
    if(!Program.FindPlacement(this,Left,Top,Width,Height,true,out destination))destination=new Point(left,top);
    if(preview==null)preview=new System.Windows.Controls.Primitives.Popup {
     Placement=System.Windows.Controls.Primitives.PlacementMode.Absolute,AllowsTransparency=true,
     IsHitTestVisible=false,StaysOpen=true,Child=new Border {BorderBrush=Brushes.Turquoise,BorderThickness=new Thickness(2),CornerRadius=new CornerRadius(28),IsHitTestVisible=false}};
    preview.Width=Width;preview.Height=card.ActualHeight;preview.HorizontalOffset=destination.X;preview.VerticalOffset=destination.Y;preview.IsOpen=true;
   };
   surface.MouseLeftButtonUp+=(s,e)=> {
    if(!dragging)return;
    dragging=false;if(preview!=null)preview.IsOpen=false;surface.ReleaseMouseCapture();
    Left=destination.X;Top=destination.Y;if(Left!=left||Top!=top){Save();Program.Remember(restore);}e.Handled=true;
   };
   surface.LostMouseCapture+=(s,e)=>cancel();
   PreviewKeyDown+=(s,e)=> {if(dragging&&e.Key==Key.Escape){cancel();e.Handled=true;}};

  }
  public void Save() {
   if(!loaded||retired)return;
   try { new XElement("Panel",new XAttribute("Mode",storeFiles?"Files":"Shortcuts"),new XAttribute("Columns",columns),new XAttribute("Rows",rows),new XAttribute("Width",Width),new XAttribute("Height",Height),new XAttribute("Left",Left),new XAttribute("Top",Top),new XAttribute("IconTheme",iconTheme),new XAttribute("IconSize",iconSize),new XAttribute("Opacity",backdropPreview?committedAlpha:alpha),new XAttribute("GlowPulse",backdropPreview?committedGlowPulse:glowPulse),new XAttribute("GlowMode",backdropPreview?committedGlowMode:glowMode),new XAttribute("OutlineColor",ColorHex(backdropPreview?committedOutlineColor:outlineColor)),new XAttribute("OutlineThickness",backdropPreview?committedOutlineThickness:outlineThickness),new XAttribute("OutlineGlow",backdropPreview?committedOutlineGlow:outlineGlow),new XAttribute("Background",ColorHex(backdropPreview?committedBackground:background)),new XAttribute("Animations",animations?"true":"false"),new XAttribute("DarkCaption",darkCaption),new XAttribute("Labels",labels?"true":"false"),new XAttribute("Topmost",Topmost?"true":"false")).Save(config); } catch {}
  }
  bool Motion { get { return animations&&SystemParameters.ClientAreaAnimation; } }
  void Fade(UIElement target,double opacity,int ms) {
   if(!Motion) { target.BeginAnimation(UIElement.OpacityProperty,null); target.Opacity=opacity; return; }
   target.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(opacity,TimeSpan.FromMilliseconds(ms)) { EasingFunction=new CubicEase { EasingMode=EasingMode.EaseOut } });
  }
  void Reveal() { if(Motion)BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(160))); else { BeginAnimation(OpacityProperty,null); Opacity=1; } }
  void IconMotion(ScaleTransform scale,TranslateTransform lift,double size,double y) {
   if(!Motion) { scale.BeginAnimation(ScaleTransform.ScaleXProperty,null); scale.BeginAnimation(ScaleTransform.ScaleYProperty,null); lift.BeginAnimation(TranslateTransform.YProperty,null); scale.ScaleX=scale.ScaleY=1; lift.Y=0; return; }
   var ease=new CubicEase { EasingMode=EasingMode.EaseOut };
   scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(size,TimeSpan.FromMilliseconds(130)) { EasingFunction=ease });
   scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(size,TimeSpan.FromMilliseconds(130)) { EasingFunction=ease });
   lift.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(y,TimeSpan.FromMilliseconds(130)) { EasingFunction=ease });
  }
  public string[] FileEntries() { return Directory.GetFileSystemEntries(folder).Where(p=>!Path.GetFileName(p).StartsWith(".gridnest-",StringComparison.OrdinalIgnoreCase)&&(File.GetAttributes(p)&(FileAttributes.Hidden|FileAttributes.System))==0).OrderBy(p=>Path.GetFileName(p),StringComparer.CurrentCultureIgnoreCase).ToArray(); }
  void LoadOrder() {
   try { order=XElement.Load(OrderFile).Elements("Item").Select(x=>(string)x.Attribute("Name")??"").ToList(); } catch { order=new List<string>(); }
  }
  void SaveOrder() {
   var xml=new XElement("Order",order.Select(n=>new XElement("Item",new XAttribute("Name",n))));
   using(var stream=new FileStream(OrderFile,FileMode.OpenOrCreate,FileAccess.Write,FileShare.Read)) { stream.SetLength(0); xml.Save(stream); }
   File.SetAttributes(OrderFile,File.GetAttributes(OrderFile)|FileAttributes.Hidden);
  }
  void SyncOrder(string[] files) {
   var names=new HashSet<string>(files.Select(Path.GetFileName),StringComparer.OrdinalIgnoreCase); var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   for(int i=0;i<order.Count;i++)if(string.IsNullOrEmpty(order[i])||!names.Contains(order[i])||!seen.Add(order[i]))order[i]="";
   foreach(var file in files) { string n=Path.GetFileName(file); if(seen.Contains(n))continue; int hole=order.FindIndex(string.IsNullOrEmpty); if(hole<0)order.Add(n);else order[hole]=n; seen.Add(n); }
   while(order.Count>0&&string.IsNullOrEmpty(order[order.Count-1]))order.RemoveAt(order.Count-1);
  }
  void PlaceAt(string fileName,int index) {
   index=Math.Max(0,Math.Min(4095,index)); int previous=order.FindIndex(n=>string.Equals(n,fileName,StringComparison.OrdinalIgnoreCase));
   if(previous==index)return;
   if(index>=order.Count||string.IsNullOrEmpty(order[index])) {
    if(previous>=0)order[previous]="";
    while(order.Count<=index)order.Add("");
    order[index]=fileName;
    while(order.Count>0&&string.IsNullOrEmpty(order[order.Count-1]))order.RemoveAt(order.Count-1);
    SaveOrder();return;
   }
   if(previous>=0)order.RemoveAt(previous);
   while(order.Count<index)order.Add("");
   order.Insert(index,fileName);
   while(order.Count>0&&string.IsNullOrEmpty(order[order.Count-1]))order.RemoveAt(order.Count-1);
   SaveOrder();
  }
  void AlignIcons() {
   Refresh();
   var previous=new List<string>(order);
   var compact=order.Where(n=>!string.IsNullOrEmpty(n)).ToList();
   if(previous.SequenceEqual(compact))return;
   order=compact;SaveOrder();Refresh();
   Program.Remember(()=> {if(retired)return;order=new List<string>(previous);Refresh();SaveOrder();});
  }
  void Refresh() {
   if(retired)return;
   icons.Children.Clear();
   string[] files;
   try { files=FileEntries(); SyncOrder(files); } catch { return; }
   int count=order.Count;
   for(int slot=0;slot<count;slot++) {
    string item=slot<order.Count?order[slot]:"";
    if(string.IsNullOrEmpty(item)) { var empty=new Border { Width=CellWidth, Height=CellHeight, Background=Brushes.Transparent, ToolTip="Свободная ячейка" }; icons.Children.Add(empty); continue; }
    string path=Path.Combine(folder,item);
    StackPanel cell=new StackPanel { Width=CellWidth, Height=CellHeight, Background=Brushes.Transparent, ToolTip=Path.GetFileName(path), Cursor=Cursors.Hand, Focusable=true };
    ImageSource source; if(!cache.TryGetValue(path,out source)) { source=ShellIcon.Get(path,iconSize*2); cache[path]=source; }
    var image=new Image { Source=source, Width=iconSize, Height=iconSize, Margin=new Thickness(0,4,0,0), Stretch=Stretch.Uniform, HorizontalAlignment=HorizontalAlignment.Center, RenderTransformOrigin=new Point(.5,.5) };
    var scale=new ScaleTransform(1,1); var lift=new TranslateTransform(); var transform=new TransformGroup(); transform.Children.Add(scale); transform.Children.Add(lift); image.RenderTransform=transform;
    cell.MouseEnter+=(s,e)=>IconMotion(scale,lift,1.08,-1); cell.MouseLeave+=(s,e)=>IconMotion(scale,lift,1,0);
    RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality); cell.Children.Add(IconTile(image,iconTheme,slot,iconSize));
    if(labels) cell.Children.Add(new TextBlock { Text=IsShortcut(path)?Path.GetFileNameWithoutExtension(path):Path.GetFileName(path), FontSize=11, Foreground=Brushes.White, TextAlignment=TextAlignment.Center, TextTrimming=TextTrimming.CharacterEllipsis, Height=16,LineHeight=16,Margin=new Thickness(0,0,0,8) });
    Point start=new Point(); bool pressed=false,dragged=false;
    cell.PreviewMouseLeftButtonDown+=(s,e)=> { if(TouchMouse(e))return;start=e.GetPosition(this); pressed=true; dragged=false; cell.Focus(); cell.CaptureMouse(); IconMotion(scale,lift,.94,1); e.Handled=true; };
    cell.MouseMove+=(s,e)=> {
     if(!pressed||e.LeftButton!=MouseButtonState.Pressed)return;
     Point now=e.GetPosition(this);
     if(Math.Abs(now.X-start.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(now.Y-start.Y)<SystemParameters.MinimumVerticalDragDistance)return;
     pressed=false; dragged=true; cell.ReleaseMouseCapture(); IconMotion(scale,lift,1,0);
     DragItem(cell,path);
     e.Handled=true;
    };
    cell.MouseLeftButtonUp+=(s,e)=> { if(TouchMouse(e))return;bool click=pressed&&!dragged; pressed=false; cell.ReleaseMouseCapture(); IconMotion(scale,lift,cell.IsMouseOver?1.08:1,cell.IsMouseOver?-1:0); if(click)Open(path); e.Handled=true; };
    cell.LostMouseCapture+=(s,e)=> { pressed=false; IconMotion(scale,lift,1,0); };
    cell.KeyDown+=(s,e)=> { if(e.Key==Key.F2) { RenameItem(path); e.Handled=true; } else if(e.Key==Key.Delete&&Keyboard.Modifiers==ModifierKeys.None) {DeleteItem(path);e.Handled=true;} };
    var menu=new ContextMenu(); Add(menu,"Открыть",()=>Open(path)); Add(menu,"Переименовать… (F2)",()=>RenameItem(path)); Add(menu,"Показать в папке",()=>Process.Start("explorer.exe","/select,\""+path+"\"")); menu.Items.Add(new Separator());var removeItem=Add(menu,"Удалить в корзину…",()=>DeleteItem(path));removeItem.InputGestureText="Delete";cell.ContextMenu=menu;AttachTouchItem(cell,path);
    icons.Children.Add(cell);
   }
  }
  [StructLayout(LayoutKind.Sequential)]
  struct CursorPoint {public int X,Y;}
  [DllImport("user32.dll")]static extern bool GetCursorPos(out CursorPoint point);
  sealed class DragIconPreview : IDisposable {
   readonly PanelWindow owner;
   readonly Point hotspot;
   readonly DispatcherTimer timer;
   Window window;
   public DragIconPreview(PanelWindow panel,FrameworkElement tile,Point? touchHotspot=null,Point? touchScreen=null) {
    owner=panel;
    double width=Math.Max(1,tile.ActualWidth),height=Math.Max(1,tile.ActualHeight);
    var point=touchHotspot??Mouse.GetPosition(tile);
    hotspot=new Point(Math.Max(0,Math.Min(width,point.X)),Math.Max(0,Math.Min(height,point.Y)));
    var drawing=new DrawingVisual();
    using(var context=drawing.RenderOpen())
     context.DrawRectangle(new VisualBrush(tile),null,new Rect(0,0,width,height));
    var bitmap=new RenderTargetBitmap((int)Math.Ceiling(width),(int)Math.Ceiling(height),96,96,PixelFormats.Pbgra32);
    bitmap.Render(drawing);bitmap.Freeze();
    window=new Window {Name="GridNestDragPreview",Owner=panel,WindowStyle=WindowStyle.None,
     AllowsTransparency=true,Background=Brushes.Transparent,ResizeMode=ResizeMode.NoResize,
     ShowInTaskbar=false,ShowActivated=false,Topmost=true,Focusable=false,IsHitTestVisible=false,
     Width=width,Height=height,Opacity=.88,
     Content=new Image {Source=bitmap,Stretch=Stretch.Fill,IsHitTestVisible=false}};
    window.SourceInitialized+=(s,e)=> {
     var handle=new WindowInteropHelper(window).Handle;
     SetWindowStyle(handle,-20,GetWindowStyle(handle,-20)|0x20|0x08000000|0x80);
    };
    timer=new DispatcherTimer(DispatcherPriority.Input) {Interval=TimeSpan.FromMilliseconds(16)};
    timer.Tick+=(s,e)=>UpdatePosition();
    window.Closed+=(s,e)=> {timer.Stop();window=null;};
    try {if(touchScreen.HasValue)UpdateTouchPosition(touchScreen.Value);else UpdatePosition();window.Show();if(!touchScreen.HasValue)timer.Start();}
    catch {Dispose();throw;}
   }
   public void UpdateTouchPosition(Point pixels) {
    if(window==null)return;var source=PresentationSource.FromVisual(owner);
    var point=source==null?pixels:source.CompositionTarget.TransformFromDevice.Transform(pixels);
    window.Left=point.X-hotspot.X;window.Top=point.Y-hotspot.Y;
   }
   public void UpdatePosition() {
    if(window==null)return;
    CursorPoint pixel;
    if(!GetCursorPos(out pixel))return;
    var source=PresentationSource.FromVisual(owner);
    var point=new Point(pixel.X,pixel.Y);
    if(source!=null)point=source.CompositionTarget.TransformFromDevice.Transform(point);
    window.Left=point.X-hotspot.X;window.Top=point.Y-hotspot.Y;
   }
   public void Dispose() {
    timer.Stop();
    if(window!=null) {window.Close();window=null;}
   }
  }
  void DragItem(StackPanel cell,string path) {
   var data=new DataObject(DataFormats.FileDrop,new[]{path});
   data.SetData(InternalDrag,path);
   DragIconPreview preview=null;
   double opacity=cell.Opacity;
   GiveFeedbackEventHandler feedback=(s,e)=> {
    if(preview!=null)preview.UpdatePosition();
    e.UseDefaultCursors=true;
   };
   try {
    try {preview=new DragIconPreview(this,(FrameworkElement)cell.Children[0]);}
    catch(Exception ex) {Debug.WriteLine(ex.Message);}
    if(preview!=null)cell.Opacity=.35;
    cell.GiveFeedback+=feedback;
    DragDrop.DoDragDrop(cell,data,DragDropEffects.Move|DragDropEffects.Copy);
   } finally {
    cell.GiveFeedback-=feedback;
    if(preview!=null)preview.Dispose();
    cell.Opacity=opacity;
    foreach(var panel in Program.Panels) {panel.cache.Clear();panel.Refresh();}
   }
  }

  static TouchDevice activeIconTouch;
  static bool TouchMouse(MouseEventArgs args) {
   return args.StylusDevice!=null&&args.StylusDevice.TabletDevice.Type==TabletDeviceType.Touch;
  }
  void AttachTouchItem(StackPanel cell,string path) {
   TouchDevice device=null;
   Point start=new Point();
   bool moved=false,dragging=false;
   double opacity=cell.Opacity;
   PanningMode panning=scroll.PanningMode;
   DragIconPreview preview=null;
   var hold=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(450)};
   Action cancel=()=> {
    hold.Stop();
    var contact=device;device=null;
    if(activeIconTouch==contact)activeIconTouch=null;
    if(dragging)scroll.PanningMode=panning;
    dragging=false;
    if(preview!=null) {preview.Dispose();preview=null;}
    cell.Opacity=opacity;
    if(contact!=null&&contact.Captured==cell)cell.ReleaseTouchCapture(contact);
   };
   hold.Tick+=(s,e)=> {
    hold.Stop();if(device==null||moved)return;
    var point=device.GetTouchPoint(this);
    if(point.Action==TouchAction.Up||(point.Position-start).Length>8) {cancel();return;}
    if(!cell.CaptureTouch(device)) {cancel();return;}
    panning=scroll.PanningMode;scroll.PanningMode=PanningMode.None;dragging=true;
    try {
     var tile=(FrameworkElement)cell.Children[0];
     preview=new DragIconPreview(this,tile,device.GetTouchPoint(tile).Position,PointToScreen(point.Position));
     cell.Opacity=.35;
    } catch(Exception ex) {Debug.WriteLine(ex.Message);}
   };
   cell.AddHandler(PreviewTouchDownEvent,new EventHandler<TouchEventArgs>((s,e)=> {
    if(activeIconTouch!=null)return;
    device=e.TouchDevice;activeIconTouch=device;start=e.GetTouchPoint(this).Position;
    moved=false;opacity=cell.Opacity;hold.Start();
   }),true);
   EventHandler<TouchEventArgs> move=(s,e)=> {
    if(device!=e.TouchDevice)return;
    var point=e.GetTouchPoint(this).Position;
    if((point-start).Length>8) {moved=true;hold.Stop();}
    if(dragging) {if(preview!=null)preview.UpdateTouchPosition(PointToScreen(point));e.Handled=true;}
   };
   EventHandler<TouchEventArgs> up=(s,e)=> {
    if(device!=e.TouchDevice)return;
    var local=e.GetTouchPoint(this).Position;
    if((local-start).Length>8)moved=true;
    bool drop=dragging&&moved,menu=dragging&&!moved,tap=!dragging&&!moved;
    Point screen=PointToScreen(local);
    cancel();
    if(drop) {
     var target=Program.Panels.Where(panel=>panel.IsVisible&&!panel.retired)
      .OrderByDescending(panel=>panel.Topmost).ThenByDescending(panel=>panel.IsActive)
      .FirstOrDefault(panel=>new Rect(0,0,panel.Width,Math.Max(0,panel.Height-38)).Contains(panel.PointFromScreen(screen)));
     if(target!=null) {
      var position=target.icons.PointFromScreen(screen);
      int index=Math.Max(0,(int)Math.Floor(position.Y/target.CellHeight))*target.columns+
       Math.Max(0,Math.Min(target.columns-1,(int)Math.Floor(position.X/target.CellWidth)));
      var errors=target.AddFiles(new[]{path},true,index);
      foreach(var panel in Program.Panels)panel.Refresh();
      if(errors.Count>0)MessageBox.Show(string.Join(Environment.NewLine,errors),"Не удалось перенести");
     }
    } else if(menu&&cell.ContextMenu!=null) {
     cell.ContextMenu.PlacementTarget=cell;cell.ContextMenu.Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom;cell.ContextMenu.IsOpen=true;
    } else if(tap)Open(path);
    e.Handled=true;
   };
   KeyEventHandler cancelKey=(s,e)=> {if(device!=null&&e.Key==Key.Escape) {cancel();e.Handled=true;}};
   AddHandler(Keyboard.PreviewKeyDownEvent,cancelKey,true);
   AddHandler(PreviewTouchMoveEvent,move,true);AddHandler(PreviewTouchUpEvent,up,true);
   cell.Unloaded+=(s,e)=> {cancel();RemoveHandler(Keyboard.PreviewKeyDownEvent,cancelKey);RemoveHandler(PreviewTouchMoveEvent,move);RemoveHandler(PreviewTouchUpEvent,up);};
   Stylus.SetIsPressAndHoldEnabled(cell,false);
  }
  void AttachTouchPanelMove(UIElement surface) {
   TouchDevice device=null;Point start=new Point();double left=0,top=0;
   Action restore=null;
   surface.TouchDown+=(s,e)=> {
    if(Program.Locked||device!=null)return;
    device=e.TouchDevice;start=PointToScreen(e.GetTouchPoint(this).Position);
    left=Left;top=Top;restore=CaptureLayout();surface.CaptureTouch(device);e.Handled=true;
   };
   surface.TouchMove+=(s,e)=> {
    if(device!=e.TouchDevice)return;
    var pixels=PointToScreen(e.GetTouchPoint(this).Position);
    var source=PresentationSource.FromVisual(this);
    var delta=source.CompositionTarget.TransformFromDevice.Transform(pixels)-source.CompositionTarget.TransformFromDevice.Transform(start);
    var area=Program.Area(left+delta.X,top+delta.Y);
    Left=Math.Max(area.Left,Math.Min(area.Right-Width,left+delta.X));Top=Math.Max(area.Top,Math.Min(area.Bottom-Height,top+delta.Y));e.Handled=true;
   };
   surface.TouchUp+=(s,e)=> {
    if(device!=e.TouchDevice)return;
    var contact=device;device=null;surface.ReleaseTouchCapture(contact);
    Point point;
    if(Program.FindPlacement(this,Left,Top,Width,Height,true,out point)) {Left=point.X;Top=point.Y;}
    else {Left=left;Top=top;}
    Save();if(Left!=left||Top!=top)Program.Remember(restore);e.Handled=true;
   };
   surface.LostTouchCapture+=(s,e)=> {if(device==e.TouchDevice) {device=null;Left=left;Top=top;Save();}};
  }
  void AttachTouchResize(UIElement surface) {
   TouchDevice device=null;Point start=new Point();double width=0,height=0;
   Action restore=null;
   surface.PreviewTouchDown+=(s,e)=> {
    if(Program.Locked||device!=null)return;
    device=e.TouchDevice;start=e.GetTouchPoint(this).Position;width=Width;height=Height;restore=CaptureLayout();
    surface.CaptureTouch(device);Fade(surface,1,100);e.Handled=true;
   };
   surface.PreviewTouchMove+=(s,e)=> {
    if(device!=e.TouchDevice)return;
    var point=e.GetTouchPoint(this).Position;SnapSize(width+point.X-start.X,height+point.Y-start.Y);e.Handled=true;
   };
   surface.PreviewTouchUp+=(s,e)=> {
    if(device!=e.TouchDevice)return;
    var contact=device;device=null;surface.ReleaseTouchCapture(contact);AlignPosition();Save();
    if(Width!=width||Height!=height)Program.Remember(restore);UpdateHandles();e.Handled=true;
   };
   surface.LostTouchCapture+=(s,e)=> {if(device==e.TouchDevice) {device=null;AlignPosition();Save();UpdateHandles();}};
  }

  void DropFiles(object sender,DragEventArgs e) {
   e.Handled=true; if(!e.Data.GetDataPresent(DataFormats.FileDrop))return;
   bool move=e.Data.GetDataPresent(InternalDrag);
   Point point=e.GetPosition(icons); int index=Math.Max(0,(int)Math.Floor(point.Y/CellHeight))*columns+Math.Max(0,Math.Min(columns-1,(int)Math.Floor(point.X/CellWidth)));
   var errors=AddFiles((string[])e.Data.GetData(DataFormats.FileDrop),move,index);
   e.Effects=errors.Count>0?DragDropEffects.None:storeFiles||move||((string[])e.Data.GetData(DataFormats.FileDrop)).All(p=>IsDesktopItem(p)&&IsShortcut(p))?DragDropEffects.Move:DragDropEffects.Copy;
   if(errors.Count>0)MessageBox.Show(string.Join("\n",errors),"Не удалось добавить",MessageBoxButton.OK,MessageBoxImage.Information);
  }
  List<string> AddFiles(string[] files,bool move=false,int position=-1) {
   List<string> errors=new List<string>();
   var reversals=new Stack<Action>();
   var priorOrders=Program.Panels.ToDictionary(panel=>panel,panel=>panel.order.ToList());
   foreach(var source in files) {
    try {
     if(Path.GetFullPath(Path.GetDirectoryName(source)).TrimEnd('\\').Equals(Path.GetFullPath(folder).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)) { if(move&&position>=0) { PlaceAt(Path.GetFileName(source),position); position++; reversals.Push(()=>{}); } continue; }
     string ext=Path.GetExtension(source); bool shortcut=File.Exists(source)&&(ext.Equals(".lnk",StringComparison.OrdinalIgnoreCase)||ext.Equals(".url",StringComparison.OrdinalIgnoreCase));
     string dest=Unique(Path.Combine(folder,shortcut?Path.GetFileName(source):Path.GetFileName(source)+".lnk"));
     bool owned=Program.Panels.Any(p=>Path.GetFullPath(Path.GetDirectoryName(source)).TrimEnd('\\').Equals(Path.GetFullPath(p.folder).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase));
     bool desktop=IsDesktopItem(source);
     if(move&&owned||storeFiles||desktop&&shortcut) { dest=Unique(Path.Combine(folder,Path.GetFileName(source))); MoveEntry(source,dest);
      var from=source;var to=dest;var created=File.GetCreationTimeUtc(to);
      reversals.Push(()=> {
       if(File.Exists(from)||Directory.Exists(from))throw new IOException("Прежнее имя занято: "+from);
       if(File.GetCreationTimeUtc(to)!=created)throw new IOException("Перемещённый объект изменился или отсутствует.");
       MoveEntry(to,from);
      }); }
     else if(shortcut)File.Copy(source,dest,false);
     else { Type t=Type.GetTypeFromProgID("WScript.Shell"); object shell=Activator.CreateInstance(t); object link=null; try { link=t.InvokeMember("CreateShortcut",System.Reflection.BindingFlags.InvokeMethod,null,shell,new object[]{dest}); link.GetType().InvokeMember("TargetPath",System.Reflection.BindingFlags.SetProperty,null,link,new object[]{source}); link.GetType().InvokeMember("Save",System.Reflection.BindingFlags.InvokeMethod,null,link,null); } finally { if(link!=null)Marshal.FinalReleaseComObject(link); Marshal.FinalReleaseComObject(shell); } }
     if(position>=0) { PlaceAt(Path.GetFileName(dest),position); position++; }
    } catch(Exception ex) { errors.Add(Path.GetFileName(source)+": "+ex.Message); }
   }
   cache.Clear(); Refresh();
   SaveOrder();
   if(reversals.Count>0) Program.Remember(()=> {
    while(reversals.Count>0) { reversals.Peek()();reversals.Pop(); }
    foreach(var pair in priorOrders) {
     if(pair.Key.retired)continue;
     pair.Key.order=pair.Value;pair.Key.Refresh();pair.Key.SaveOrder();
    }
   });
   return errors;
  }
  static bool IsDesktopItem(string path) { string parent=Path.GetFullPath(Path.GetDirectoryName(path)).TrimEnd('\\'); return new[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)}.Any(p=>parent.Equals(Path.GetFullPath(p).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)); }
  static bool IsShortcut(string path) { string ext=Path.GetExtension(path); return ext.Equals(".lnk",StringComparison.OrdinalIgnoreCase)||ext.Equals(".url",StringComparison.OrdinalIgnoreCase); }
  static void MoveEntry(string source,string dest) {
   string s=Path.GetFullPath(source).TrimEnd('\\'),d=Path.GetFullPath(dest).TrimEnd('\\');
   if(Directory.Exists(source)) {
    if(d.StartsWith(s+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Нельзя переместить папку внутрь самой себя.");
    if(string.Equals(Path.GetPathRoot(s),Path.GetPathRoot(d),StringComparison.OrdinalIgnoreCase))Directory.Move(source,dest);
    else {
     if(File.Exists(dest)||Directory.Exists(dest))throw new IOException("Имя назначения занято.");
     Microsoft.VisualBasic.FileIO.FileSystem.MoveDirectory(source,dest,
      Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException);
    }
   } else File.Move(source,dest);
  }
  void SetIconSize(int pixels) {var restore=CaptureLayout();int old=iconSize;iconSize=Math.Max(28,Math.Min(128,pixels));if(!SetGrid(columns,rows)) {iconSize=old;return;}Clamp();AlignPosition();cache.Clear();Refresh();Save();if(iconSize!=old)Program.Remember(restore);}
  static string Unique(string path) { if(!File.Exists(path)&&!Directory.Exists(path))return path; string d=Path.GetDirectoryName(path), n=Path.GetFileNameWithoutExtension(path), e=Path.GetExtension(path); for(int i=2;;i++) { string p=Path.Combine(d,n+" ("+i+")"+e); if(!File.Exists(p)&&!Directory.Exists(p))return p; } }
  static string RenamedPath(string source,string newName) {
   if(string.IsNullOrWhiteSpace(newName)||newName!=newName.Trim()||newName.EndsWith(".")||newName.IndexOfAny(Path.GetInvalidFileNameChars())>=0||newName=="."||newName=="..")throw new IOException("Недопустимое имя. Уберите запрещённые символы, пробелы по краям и точку в конце.");
   string stem=newName.Split('.')[0].ToUpperInvariant();
   if(new[]{"CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9","LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"}.Contains(stem))throw new IOException("Это зарезервированное имя Windows.");
   string ext=Directory.Exists(source)?"":Path.GetExtension(source);
   if(ext.Length>0&&!newName.EndsWith(ext,StringComparison.OrdinalIgnoreCase))newName+=ext;
   return Path.Combine(Path.GetDirectoryName(source),newName);
  }
  static string RenamePath(string source,string newName) {
   string dest=RenamedPath(source,newName);
   if(source.Equals(dest,StringComparison.Ordinal))return source;
   if(!source.Equals(dest,StringComparison.OrdinalIgnoreCase)&&(File.Exists(dest)||Directory.Exists(dest)))throw new IOException("Такое имя уже существует. Выберите другое.");
   if(Directory.Exists(source))Directory.Move(source,dest);else File.Move(source,dest);
   return dest;
  }
  void AskName(string title,string value,Action<string> apply) {
   var w=new Window { Title=title, Width=420, Height=220, ResizeMode=ResizeMode.NoResize, WindowStartupLocation=WindowStartupLocation.CenterOwner, Owner=this, ShowInTaskbar=false };
   var layout=new SettingsLayout(w);var sp=layout.Body;
   var input=new TextBox { Text=value, Margin=new Thickness(0,0,0,14) }; sp.Children.Add(input);
   var ok=new Button { Content="Переименовать", IsDefault=true, Padding=new Thickness(10,6,10,6) }; SettingsLayout.Buttons(layout.Actions,w,ok);
   ok.Click+=(s,e)=> { try { apply(input.Text); w.Close(); } catch(Exception ex) { MessageBox.Show(w,ex.Message,"Не удалось переименовать",MessageBoxButton.OK,MessageBoxImage.Information); } };
   w.Loaded+=(s,e)=> { input.Focus(); input.SelectAll(); }; w.ShowDialog();
  }
  string ValidateItemPath(string path) {
   string full=Path.GetFullPath(path);
   string parent=(Path.GetDirectoryName(full)??"").TrimEnd(Path.DirectorySeparatorChar);
   if(!parent.Equals(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)||
    Path.GetFileName(full).StartsWith(".gridnest-",StringComparison.OrdinalIgnoreCase))
    throw new IOException("Выберите объект внутри этой папки.");
   if(!File.Exists(full)&&!Directory.Exists(full))throw new IOException("Объект уже перемещён или удалён.");
   return full;
  }
  void DeleteItem(string path) {
   try {path=ValidateItemPath(path);}catch(Exception ex) {MessageBox.Show(ex.Message,"Не удалось удалить");return;}
   bool directory=Directory.Exists(path);
   var w=new Window {Title="Удалить из папки — GridNest",Width=460,Height=280,Owner=this,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.CenterOwner};
   var layout=new SettingsLayout(w);
   layout.Body.Children.Add(new TextBlock {Text=Path.GetFileName(path),FontSize=17,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)});
   SettingsLayout.Hint(layout.Body,directory?"Вложенная папка с её содержимым будет перемещена в корзину.":IsShortcut(path)?"В корзину будет перемещён только этот ярлык.":"Файл будет перемещён в корзину.");
   var remove=new Button {Content="Удалить в корзину"};
   SettingsLayout.Buttons(layout.Actions,w,remove);
   remove.Click+=(s,e)=> {
    remove.IsEnabled=false;
    try {
     string selected=ValidateItemPath(path);
     if(Directory.Exists(selected))Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(selected,
      Microsoft.VisualBasic.FileIO.UIOption.AllDialogs,Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException);
     else Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(selected,
      Microsoft.VisualBasic.FileIO.UIOption.AllDialogs,Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException);
     cache.Clear();Refresh();SaveOrder();Program.Remember(null);w.Close();
    } catch(OperationCanceledException) {w.Close();}
    catch(Exception ex) {MessageBox.Show(w,ex.Message,"Не удалось удалить");}
    finally {if(w.IsVisible)remove.IsEnabled=true;}
   };
   w.ShowDialog();
  }

  void RenameItem(string path) { AskName("Переименовать значок",Directory.Exists(path)?Path.GetFileName(path):Path.GetFileNameWithoutExtension(path),value=> { string dest=RenamePath(path,value); for(int i=0;i<order.Count;i++)if(order[i].Equals(Path.GetFileName(path),StringComparison.OrdinalIgnoreCase))order[i]=Path.GetFileName(dest); SaveOrder(); cache.Clear(); Refresh(); }); }
  public static string ValidateFolderName(string value) { if(value.StartsWith("."))throw new IOException("Имя папки не должно начинаться с точки."); return RenamedPath(Path.Combine(Program.Data,"Folder"),value); }
  string[] TransferEntries() {
   return Directory.GetFileSystemEntries(folder).Where(path=>
    !path.Equals(OrderFile,StringComparison.OrdinalIgnoreCase)&&
    !Path.GetFileName(path).Equals("desktop.ini",StringComparison.OrdinalIgnoreCase)).ToArray();
  }
  List<string> MoveContentsOut(string destination) {
   var errors=new List<string>();
   string target=Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar);
   string sourceFolder=Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar);
   if(target.Equals(sourceFolder,StringComparison.OrdinalIgnoreCase)||
    target.StartsWith(sourceFolder+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
    throw new IOException("Место назначения должно находиться за пределами папки.");
   Directory.CreateDirectory(target);
   watcher.EnableRaisingEvents=false;
   try {
    foreach(string source in TransferEntries()) {
     try {MoveEntry(source,Unique(Path.Combine(target,Path.GetFileName(source))));}
     catch(Exception ex) {errors.Add(Path.GetFileName(source)+": "+ex.Message);}
    }
    cache.Clear();Refresh();SaveOrder();
    if(errors.Count==0) {
     if(TransferEntries().Length>0)errors.Add("В папке появились новые объекты. Повторите перенос.");
     else {try {Archive();}catch(Exception ex) {errors.Add(ex.Message);}}
    }
   } finally {if(!retired)watcher.EnableRaisingEvents=true;}
   return errors;
  }
  void RemoveToDesktop() {
   var w=new Window {Title="Перенести на рабочий стол — GridNest",Width=460,Height=290,Owner=this,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.CenterOwner};
   var layout=new SettingsLayout(w);
   layout.Body.Children.Add(new TextBlock {Text="Убрать папку «"+name+"»?",TextWrapping=TextWrapping.Wrap,FontSize=17,Margin=new Thickness(0,0,0,12)});
   SettingsLayout.Hint(layout.Body,"Ярлыки, файлы и вложенные папки будут перемещены на рабочий стол. При совпадении имён будет добавлен номер.");
   var move=new Button {Content="На рабочий стол"};
   SettingsLayout.Buttons(layout.Actions,w,move);
   move.Click+=(s,e)=> {
    move.IsEnabled=false;
    try {
     var errors=MoveContentsOut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
     if(errors.Count==0) {if(w.IsVisible)w.Close();}
     else MessageBox.Show(w,"Часть объектов осталась в папке. Можно повторить перенос."+Environment.NewLine+Environment.NewLine+string.Join(Environment.NewLine,errors),"Перенос не завершён");
    } catch(Exception ex) {MessageBox.Show(w,ex.Message,"Не удалось перенести");}
    finally {if(w.IsVisible)move.IsEnabled=true;}
   };
   w.ShowDialog();
  }

  public string Archive() {
   Save(); SaveOrder();
   string entry=Path.Combine(Program.Data,".archive",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N"));
   string dest=Path.Combine(entry,name); Directory.CreateDirectory(entry);
   if(File.Exists(config))File.Copy(config,Path.Combine(entry,"panel.xml"));
   watcher.EnableRaisingEvents=false;
   try { Directory.Move(folder,dest); } catch { watcher.EnableRaisingEvents=true; throw; }
   retired=true; Program.Panels.Remove(this); Close(); return dest;
  }
  public static void OpenPath(string path) { Open(path); }
  void RenameFolder() {
   AskName("Переименовать папку",name,value=> {
    if(value.StartsWith("."))throw new IOException("Имя папки не должно начинаться с точки.");
    string dest=RenamedPath(folder,value); if(dest==folder)return;
    string next=Path.Combine(Program.Home,value+".xml"); if(File.Exists(next))throw new IOException("Настройки папки с таким именем уже существуют.");
    string oldFolder=folder, oldConfig=config; bool movedDir=false,movedConfig=false;
    watcher.EnableRaisingEvents=false;
    try { Directory.Move(folder,dest); movedDir=true; if(File.Exists(config)) { File.Move(config,next); movedConfig=true; } watcher.Path=dest; folder=dest; config=next; name=value; caption.Text=value; Title="GridNest — "+name; Save(); }
    catch { if(movedConfig)File.Move(next,oldConfig); if(movedDir)Directory.Move(dest,oldFolder); watcher.Path=oldFolder; throw; }
    finally { watcher.EnableRaisingEvents=true; }
   });
  }
  static void Open(string path) { try { Process.Start(new ProcessStartInfo(path) { UseShellExecute=true }); } catch(Exception e) { MessageBox.Show(e.Message,"Не удалось открыть",MessageBoxButton.OK,MessageBoxImage.Information); } }
  static string ColorHex(Color c) { return "#"+c.R.ToString("X2")+c.G.ToString("X2")+c.B.ToString("X2"); }
  static Color ParseColor(string text) {
   text=(text??"").Trim(); if(!System.Text.RegularExpressions.Regex.IsMatch(text,"^#?[0-9a-fA-F]{6}$"))throw new FormatException("Введите цвет в формате #RRGGBB, например #24425C.");
   return (Color)ColorConverter.ConvertFromString(text.StartsWith("#")?text:"#"+text);
  }
  void UpdateBackground() { UpdateOutline();if(card!=null)card.Background=new SolidColorBrush(Color.FromArgb((byte)(alpha*255),background.R,background.G,background.B));double luma=.2126*background.R+.7152*background.G+.0722*background.B;Resources["PanelHandle"]=new SolidColorBrush(luma>150?Color.FromRgb(39,87,97):Color.FromRgb(167,214,213)); }
  sealed class DialogOwner : Forms.IWin32Window { public IntPtr Handle { get; private set; } public DialogOwner(Window w) { Handle=new WindowInteropHelper(w).Handle; } }
  string iconTheme="Original";
  Color outlineColor=Color.FromRgb(138,229,213);
  double outlineThickness,outlineGlow;
  Border outlineLayer,glowLayer;
  bool outlineColorValid=true;
  Color committedOutlineColor;
  double committedOutlineThickness,committedOutlineGlow;
  string glowMode="Inside",committedGlowMode;
  bool glowPulse,committedGlowPulse;
  System.Windows.Media.Effects.DropShadowEffect GlowEffect() {
   double strength=outlineGlow/100;
   var effect=new System.Windows.Media.Effects.DropShadowEffect {Color=outlineColor,BlurRadius=12+outlineGlow*.15,ShadowDepth=0,Opacity=strength};
   if(glowPulse&&Motion)effect.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty,
    new DoubleAnimation(strength*.35,strength,TimeSpan.FromSeconds(1.1)) {
     AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,EasingFunction=new SineEase {EasingMode=EasingMode.EaseInOut}
    });
   return effect;
  }
  Window haloWindow;
  Grid haloFrame;
  Border haloBorder;
  const double HaloPadding=32;
  static string ValidGlowMode(string value) {
   return new[]{"Inside","Outside","Both"}.Contains(value)?value:"Inside";
  }
  [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int GetWindowStyle(IntPtr handle,int index);
  [DllImport("user32.dll",EntryPoint="SetWindowLongW")]static extern int SetWindowStyle(IntPtr handle,int index,int value);
  void PositionOuterGlow() {
   if(haloWindow==null)return;
   double bodyHeight=Math.Max(1,Height-38);
   haloWindow.Left=Left-HaloPadding;haloWindow.Top=Top-HaloPadding;
   haloWindow.Width=Width+HaloPadding*2;haloWindow.Height=bodyHeight+HaloPadding*2;
   var bounds=new RectangleGeometry(new Rect(0,0,haloWindow.Width,haloWindow.Height));
   var panel=new RectangleGeometry(new Rect(HaloPadding,HaloPadding,Width,bodyHeight),30,30);
   haloFrame.Clip=glowMode=="Outside"?new CombinedGeometry(GeometryCombineMode.Exclude,bounds,panel):(Geometry)bounds;
  }
  void UpdateOuterGlow() {
   bool show=loaded&&IsVisible&&!retired&&outlineThickness>0&&outlineGlow>0&&glowMode!="Inside";
   if(!show) {if(haloWindow!=null)haloWindow.Hide();return;}
   if(haloWindow==null) {
    haloBorder=new Border {CornerRadius=new CornerRadius(30),Margin=new Thickness(HaloPadding),IsHitTestVisible=false};
    haloFrame=new Grid {IsHitTestVisible=false};haloFrame.Children.Add(haloBorder);
    haloWindow=new Window {Owner=this,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,
     AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=false,ShowActivated=false,
     Focusable=false,IsHitTestVisible=false,Content=haloFrame};
    haloWindow.SetBinding(OpacityProperty,new System.Windows.Data.Binding("Opacity") {Source=this});
    haloWindow.SourceInitialized+=(s,e)=> {
     var handle=new WindowInteropHelper(haloWindow).Handle;
     // Layered decoration passes mouse input through and never activates.
     SetWindowStyle(handle,-20,GetWindowStyle(handle,-20)|0x20|0x08000000|0x80);
    };
    haloWindow.Closed+=(s,e)=>haloWindow=null;
   }
   haloBorder.BorderBrush=new SolidColorBrush(outlineColor);
   haloBorder.BorderThickness=new Thickness(outlineThickness);
   haloBorder.Effect=GlowEffect();
   PositionOuterGlow();
   if(!haloWindow.IsVisible)haloWindow.Show();
  }

  void UpdateOutline() {
   if(outlineLayer==null)return;
   outlineLayer.BorderBrush=new SolidColorBrush(outlineColor);
   outlineLayer.BorderThickness=new Thickness(outlineThickness);
   outlineLayer.Visibility=outlineThickness>0?Visibility.Visible:Visibility.Collapsed;
   glowLayer.BorderBrush=new SolidColorBrush(outlineColor);
   glowLayer.BorderThickness=new Thickness(outlineThickness);
   glowLayer.Visibility=outlineThickness>0&&outlineGlow>0&&glowMode=="Inside"?Visibility.Visible:Visibility.Collapsed;
   glowLayer.Effect=outlineGlow>0?GlowEffect():null;
   UpdateOuterGlow();
  }
  void OutlineEditor(StackPanel parent) {
   parent.Children.Add(new TextBlock {Text="Направление свечения",Margin=new Thickness(0,0,0,6)});
   var directions=new[]{"Inside","Outside","Both"};
   var mode=new ComboBox {Name="GlowDirection",ItemsSource=new[]{"Внутрь","Наружу","С обеих сторон"},SelectedIndex=Array.IndexOf(directions,glowMode),Margin=new Thickness(0,0,0,14)};
   parent.Children.Add(mode);mode.SelectionChanged+=(s,e)=> {glowMode=directions[mode.SelectedIndex];UpdateOutline();};
   var pulse=new CheckBox {Name="GlowPulse",Content="Пульсация свечения",IsChecked=glowPulse,Margin=new Thickness(0,0,0,12)};
   parent.Children.Add(pulse);pulse.Click+=(s,e)=> {glowPulse=pulse.IsChecked==true;UpdateOutline();};
   parent.Children.Add(new TextBlock {Text="Цвет обводки",Margin=new Thickness(0,0,0,6)});
   var input=new TextBox {Name="OutlineColor",Text=ColorHex(outlineColor),ToolTip="Цвет в формате #RRGGBB"};
   parent.Children.Add(input);
   AddColorPresets(parent,input);
   var error=new TextBlock {Text="Введите цвет в формате #RRGGBB",Foreground=Brushes.Salmon,Visibility=Visibility.Collapsed};
   parent.Children.Add(error);
   input.TextChanged+=(s,e)=> {
    try {outlineColor=ParseColor(input.Text);outlineColorValid=true;error.Visibility=Visibility.Collapsed;UpdateOutline();}
    catch(FormatException) {outlineColorValid=false;error.Visibility=Visibility.Visible;}
   };
   var choose=new Button {Content="Выбрать цвет…",Margin=new Thickness(0,8,0,12)};
   parent.Children.Add(choose);
   choose.Click+=(s,e)=> {
    using(var dialog=new Forms.ColorDialog {FullOpen=true,Color=System.Drawing.Color.FromArgb(outlineColor.R,outlineColor.G,outlineColor.B)}) {
     if(dialog.ShowDialog(new DialogOwner(Window.GetWindow(parent)))==Forms.DialogResult.OK)
      input.Text=ColorHex(Color.FromRgb(dialog.Color.R,dialog.Color.G,dialog.Color.B));
    }
   };
   var thicknessLabel=new TextBlock();parent.Children.Add(thicknessLabel);
   var thickness=new Slider {Name="OutlineThickness",Minimum=0,Maximum=8,TickFrequency=.5,IsSnapToTickEnabled=true,Value=outlineThickness};
   parent.Children.Add(thickness);
   var glowLabel=new TextBlock();parent.Children.Add(glowLabel);
   var glow=new Slider {Name="OutlineGlow",Minimum=0,Maximum=100,TickFrequency=1,IsSnapToTickEnabled=true,Value=outlineGlow};
   parent.Children.Add(glow);
   Action update=()=> {
    outlineThickness=thickness.Value;outlineGlow=glow.Value;
    thicknessLabel.Text="Толщина · "+outlineThickness.ToString("0.#")+" px";
    glowLabel.Text="Свечение · "+Math.Round(outlineGlow)+"%";
    UpdateOutline();
   };
   thickness.ValueChanged+=(s,e)=>update();glow.ValueChanged+=(s,e)=>update();update();
   SettingsLayout.Hint(parent,"Толщина 0 отключает обводку. Свечение 0 отключает подсветку.");
  }

  static ImageSource TrimIcon(ImageSource source) {
   var bitmap=source as BitmapSource;
   if(bitmap==null||bitmap.PixelWidth>512||bitmap.PixelHeight>512)return source;
   try {
    var pixels=new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0);
    int w=pixels.PixelWidth,h=pixels.PixelHeight,stride=w*4;
    var bytes=new byte[stride*h];pixels.CopyPixels(bytes,stride,0);
    int left=w,top=h,right=-1,bottom=-1;
    for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
     if(bytes[y*stride+x*4+3]<24)continue;
     left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
    }
    if(right<left)return source;
    var cropped=new CroppedBitmap(pixels,new Int32Rect(left,top,right-left+1,bottom-top+1));cropped.Freeze();return cropped;
   } catch {return source;}
  }
  FrameworkElement IconTile(Image image,string theme,int index,double size) {
   var grid=new Grid {Width=size,Height=size,Margin=new Thickness(0,8,0,labels?4:8),HorizontalAlignment=HorizontalAlignment.Center};
   bool pastel=theme=="Pastel";
   if(theme!="Original") {
    var colors=new[]{Color.FromRgb(235,215,247),Color.FromRgb(202,235,223),Color.FromRgb(250,224,198),Color.FromRgb(209,224,250)};
    Brush fill=pastel?new SolidColorBrush(colors[index%colors.Length]):
     (Brush)new LinearGradientBrush(Color.FromArgb(92,223,241,255),Color.FromArgb(28,114,146,173),90);
    grid.Children.Add(new Border {CornerRadius=new CornerRadius(size*.23),Background=fill,
     BorderBrush=new SolidColorBrush(pastel?Color.FromArgb(100,255,255,255):Color.FromArgb(90,223,241,255)),BorderThickness=new Thickness(1)});
    image.Source=TrimIcon(image.Source);image.Width=image.Height=size*.70;
   }
   image.Margin=new Thickness(0);image.VerticalAlignment=VerticalAlignment.Center;
   grid.Children.Add(image);
   return grid;
  }
  public void RefreshIconTheme() {Refresh();}
  void AddThemePicker(StackPanel parent,Action<string> selected) {
   parent.Children.Add(new TextBlock {Text="Стиль значков",Margin=new Thickness(0,8,0,4)});
   var themes=new[]{"Original","Glass","Pastel"};
   var picker=new ComboBox {ItemsSource=new[]{"Исходные значки","Мягкое стекло","Пастель"},SelectedIndex=Array.IndexOf(themes,iconTheme)};
   parent.Children.Add(picker);
   var preview=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,10,0,14)};
   parent.Children.Add(preview);
   var samples=FileEntries().Take(3).ToArray();
   Action render=()=> {
    preview.Children.Clear();
    for(int i=0;i<3;i++) {
     var path=samples.Length>i?samples[i]:folder;
     var tile=IconTile(new Image {Source=ShellIcon.Get(path,96),Width=48,Height=48,Stretch=Stretch.Uniform},themes[picker.SelectedIndex],i,48);
     tile.Margin=new Thickness(0,0,12,0);preview.Children.Add(tile);
    }
    selected(themes[picker.SelectedIndex]);
   };
   picker.SelectionChanged+=(s,e)=>render();render();
  }

  static string ValidIconTheme(string value) {return new[]{"Original","Glass","Pastel"}.Contains(value)?value:"Original";}
  sealed class FolderStyle {
   public string IconTheme="Original";
   public string GlowMode="Inside";
   public bool GlowPulse;
   public Color OutlineColor=Color.FromRgb(138,229,213);
   public double OutlineThickness,OutlineGlow;
   public Color Background;
   public double Opacity;
   public bool DarkText, Labels, Animations;
   public int IconSize, Columns, Rows;
   public XElement ToXml() {
    return new XElement("Style",new XAttribute("GlowPulse",GlowPulse),new XAttribute("GlowMode",GlowMode),new XAttribute("OutlineColor",ColorHex(OutlineColor)),new XAttribute("OutlineThickness",OutlineThickness),new XAttribute("OutlineGlow",OutlineGlow),new XAttribute("Background",ColorHex(Background)),
     new XAttribute("Opacity",Opacity),new XAttribute("DarkText",DarkText),
     new XAttribute("Labels",Labels),new XAttribute("Animations",Animations),
     new XAttribute("IconTheme",IconTheme),new XAttribute("IconSize",IconSize),new XAttribute("Columns",Columns),new XAttribute("Rows",Rows));
   }
   public static FolderStyle Read(XElement x) {
    return new FolderStyle { GlowPulse=(bool?)x.Attribute("GlowPulse")??false,GlowMode=ValidGlowMode((string)x.Attribute("GlowMode")),OutlineColor=ParseColor((string)x.Attribute("OutlineColor")??"#8AE5D5"),OutlineThickness=Math.Max(0,Math.Min(8,Get(x,"OutlineThickness",0))),OutlineGlow=Math.Max(0,Math.Min(100,Get(x,"OutlineGlow",0))),Background=ParseColor((string)x.Attribute("Background")),IconTheme=ValidIconTheme((string)x.Attribute("IconTheme")),
     Opacity=Math.Max(0,Math.Min(1,(double)x.Attribute("Opacity"))),
     DarkText=(bool)x.Attribute("DarkText"),Labels=(bool)x.Attribute("Labels"),
     Animations=(bool)x.Attribute("Animations"),
     IconSize=Math.Max(28,Math.Min(128,(int)x.Attribute("IconSize"))),
     Columns=Math.Max(1,Math.Min(20,(int)x.Attribute("Columns"))),
     Rows=Math.Max(1,Math.Min(20,(int)x.Attribute("Rows"))) };
   }
  }
  static FolderStyle copiedStyle;
  FolderStyle CurrentStyle() {
   return new FolderStyle {GlowPulse=glowPulse,GlowMode=glowMode,OutlineColor=outlineColor,OutlineThickness=outlineThickness,OutlineGlow=outlineGlow,IconTheme=iconTheme,Background=background,Opacity=alpha,DarkText=darkCaption,
    Labels=labels,Animations=animations,IconSize=iconSize,Columns=columns,Rows=rows };
  }
  void RedrawStyle() {
   UpdateBackground();PaintLabel();animationMenu.IsChecked=animations;
   cache.Clear();Refresh();Save();
  }
  void RestoreStyle(FolderStyle style,Point position) {
   glowPulse=style.GlowPulse;glowMode=style.GlowMode;outlineColor=style.OutlineColor;outlineThickness=style.OutlineThickness;outlineGlow=style.OutlineGlow;
   background=style.Background;alpha=style.Opacity;darkCaption=style.DarkText;iconTheme=style.IconTheme;
   labels=style.Labels;animations=style.Animations;iconSize=style.IconSize;
   columns=style.Columns;rows=style.Rows;Left=position.X;Top=position.Y;
   snapping=true;
   try { Width=32+columns*CellWidth;Height=70+rows*CellHeight;icons.Width=columns*CellWidth; }
   finally { snapping=false; }
   RedrawStyle();
  }
  bool ReceiveStyle(FolderStyle style,bool appearance,bool items,bool dimensions,bool? transparency=null,bool outline=false) {
   bool layoutFits=true;
   var previous=CurrentStyle();var position=new Point(Left,Top);
   if(items) {labels=style.Labels;iconSize=style.IconSize;}
   if(items||dimensions) {
    int c=dimensions?style.Columns:columns,r=dimensions?style.Rows:rows;
    if(!SetGrid(c,r,false)||columns!=c||rows!=r) {
     RestoreStyle(previous,position);layoutFits=false;
    }
   }
   if(outline) {glowPulse=style.GlowPulse;glowMode=style.GlowMode;outlineColor=style.OutlineColor;outlineThickness=style.OutlineThickness;outlineGlow=style.OutlineGlow;}
   if(transparency??appearance)alpha=style.Opacity;
   if(appearance) {background=style.Background;darkCaption=style.DarkText;iconTheme=style.IconTheme;}
   if(items)animations=style.Animations;
   RedrawStyle();return layoutFits;
  }
  void LoadDefaultStyle() {
   if(File.Exists(config))return;
   try {
    var style=FolderStyle.Read(XElement.Load(Path.Combine(Program.Home,"default-style.xml")));
    glowPulse=style.GlowPulse;glowMode=style.GlowMode;outlineColor=style.OutlineColor;outlineThickness=style.OutlineThickness;outlineGlow=style.OutlineGlow;
   background=style.Background;alpha=style.Opacity;darkCaption=style.DarkText;iconTheme=style.IconTheme;
    labels=style.Labels;animations=style.Animations;iconSize=style.IconSize;
    columns=style.Columns;rows=style.Rows;
   } catch(Exception ex) { Debug.WriteLine(ex.Message); }
  }
  void ShareStyle(bool paste=false) {
   if(paste&&copiedStyle==null) {MessageBox.Show("Сначала скопируйте оформление папки.");return;}
   var source=paste?copiedStyle:CurrentStyle();
   var w=new Window {Title=paste?"Вставить оформление — GridNest":"Применить к другим папкам — GridNest",
    Width=520,Height=Math.Min(580,Program.Area(Left,Top).Height-40),Owner=this,ShowInTaskbar=false,
    WindowStartupLocation=WindowStartupLocation.CenterOwner};
   var layout=new SettingsLayout(w);
   var checks=new Dictionary<PanelWindow,CheckBox>();
   CheckBox all=null;
   if(!paste) {
    var recipients=layout.Page("Папки");
    SettingsLayout.Hint(recipients,"Оформление папки «"+name+"»");
    all=new CheckBox {Content="Выбрать все",Name="SelectAllFolders",Margin=new Thickness(0,0,0,10)};
    recipients.Children.Add(all);
    var targets=new StackPanel {Margin=new Thickness(10,4,16,4)};
    foreach(var panel in Program.Panels.Where(panel=>panel!=this&&!panel.retired)) {
     var check=new CheckBox {Content=panel.FolderName,ToolTip=panel.FolderName};
     checks.Add(panel,check);targets.Children.Add(check);
    }
    var list=new ScrollViewer {Name="FolderRecipients",Content=targets,Height=260,
     HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,PanningMode=PanningMode.VerticalOnly};
    var listFrame=new Border {Child=list,CornerRadius=new CornerRadius(7),BorderThickness=new Thickness(1)};
    listFrame.SetResourceReference(Border.BorderBrushProperty,"SettingsLine");
    recipients.Children.Add(listFrame);
    if(checks.Count==0)SettingsLayout.Hint(recipients,"Других папок пока нет. Можно сохранить оформление для новых папок.");
   } else {
    checks.Add(this,new CheckBox {IsChecked=true});
    SettingsLayout.Hint(layout.Body,"Оформление будет применено к папке «"+name+"».");
   }
   var options=paste?layout.Body:layout.Page("Параметры");
   var appearance=new CheckBox {Name="ShareAppearance",Content="Стиль значков, цвет и название",IsChecked=true};
   var transparency=new CheckBox {Name="ShareTransparency",Content="Прозрачность подложки",IsChecked=true};
   var outline=new CheckBox {Name="ShareOutline",Content="Обводка и свечение",IsChecked=true};
   var items=new CheckBox {Content="Масштаб значков, подписи и анимации"};
   var dimensions=new CheckBox {Content="Число столбцов и рядов"};
   options.Children.Add(appearance);options.Children.Add(transparency);options.Children.Add(outline);options.Children.Add(items);options.Children.Add(dimensions);
   var future=options;future.Children.Add(new Separator {Margin=new Thickness(0,12,0,12)});
   var defaults=new CheckBox {Name="SaveDefaultStyle",Content="Использовать выбранные параметры для новых папок"};
   future.Children.Add(defaults);
   var summary=new TextBlock {Margin=new Thickness(0,0,0,12),TextWrapping=TextWrapping.Wrap};
   layout.Actions.Children.Add(summary);
   var apply=new Button {Name="ApplySharedStyle",Content="Применить"};
   SettingsLayout.Buttons(layout.Actions,w,apply);
   bool selectingAll=false;
   Action updateSummary=()=> {
    int count=checks.Count(pair=>pair.Value.IsChecked==true);
    summary.Text="Выбрано папок: "+count+" из "+checks.Count;
    if(defaults.IsChecked==true)summary.Text+=" · Стиль новых папок";
    apply.IsEnabled=(count>0||defaults.IsChecked==true)&&
     (appearance.IsChecked==true||transparency.IsChecked==true||outline.IsChecked==true||items.IsChecked==true||dimensions.IsChecked==true);
    if(all!=null&&!selectingAll)all.IsChecked=count==0?(bool?)false:count==checks.Count?(bool?)true:null;
   };
   if(all!=null)all.Click+=(s,e)=> {
    bool selected=all.IsChecked!=false;
    selectingAll=true;
    try {foreach(var check in checks.Values)check.IsChecked=selected;}
    finally {selectingAll=false;}
    updateSummary();
   };
   foreach(var check in checks.Values.Concat(new[]{appearance,transparency,outline,items,dimensions,defaults})) {
    check.Checked+=(s,e)=>updateSummary();check.Unchecked+=(s,e)=>updateSummary();
   }
   updateSummary();

   apply.Click+=(s,e)=> {
    var selected=checks.Where(pair=>pair.Value.IsChecked==true).Select(pair=>pair.Key).ToList();
    if(!selected.Any()&&defaults.IsChecked!=true) {MessageBox.Show(w,"Выберите папки.");return;}
    if(appearance.IsChecked!=true&&transparency.IsChecked!=true&&outline.IsChecked!=true&&items.IsChecked!=true&&dimensions.IsChecked!=true) {MessageBox.Show(w,"Выберите параметры.");return;}
    var snapshots=selected.ToDictionary(panel=>panel,panel=>panel.CurrentStyle());
    var positions=selected.ToDictionary(panel=>panel,panel=>new Point(panel.Left,panel.Top));
    var failed=new List<PanelWindow>();
    foreach(var panel in selected)
     if(!panel.ReceiveStyle(source,appearance.IsChecked==true,items.IsChecked==true,dimensions.IsChecked==true,transparency.IsChecked==true,outline.IsChecked==true))failed.Add(panel);
    if(failed.Count>0)MessageBox.Show(w,"Размер или масштаб не применён к папкам\n"+string.Join(", ",failed.Select(panel=>panel.FolderName))+"\nВыбранные цвета и прозрачность применены.","Недостаточно места или папки закреплены");
    string defaultPath=Path.Combine(Program.Home,"default-style.xml");
    string oldDefault=File.Exists(defaultPath)?File.ReadAllText(defaultPath):null;
    if(defaults.IsChecked==true) {
     var initial=new FolderStyle {Background=Color.FromRgb(44,46,55),Opacity=.84,IconSize=64,Columns=3,Rows=3,Animations=true};
     if(oldDefault!=null) {try {initial=FolderStyle.Read(XElement.Parse(oldDefault));}catch{}}
     if(appearance.IsChecked==true) {initial.IconTheme=source.IconTheme;initial.Background=source.Background;initial.DarkText=source.DarkText;}
     if(outline.IsChecked==true) {initial.GlowPulse=source.GlowPulse;initial.GlowMode=source.GlowMode;initial.OutlineColor=source.OutlineColor;initial.OutlineThickness=source.OutlineThickness;initial.OutlineGlow=source.OutlineGlow;}
     if(transparency.IsChecked==true)initial.Opacity=source.Opacity;
     if(items.IsChecked==true) {initial.IconSize=source.IconSize;initial.Labels=source.Labels;initial.Animations=source.Animations;}
     if(dimensions.IsChecked==true) {initial.Columns=source.Columns;initial.Rows=source.Rows;}
     try {initial.ToXml().Save(defaultPath);}catch(Exception ex) {MessageBox.Show(w,ex.Message,"Не удалось сохранить стиль");}
    }
    Program.Remember(()=> {
     foreach(var panel in selected)if(!panel.retired)panel.RestoreStyle(snapshots[panel],positions[panel]);
     if(defaults.IsChecked==true) {if(oldDefault==null) {if(File.Exists(defaultPath))File.Delete(defaultPath);}else File.WriteAllText(defaultPath,oldDefault);}
    });
    w.Close();
   };
   w.ShowDialog();
  }

  bool backdropPreview;
  bool backdropColorValid=true;
  Color committedBackground;
  double committedAlpha;
  void BeginBackdropPreview() {
   committedBackground=background;committedAlpha=alpha;committedGlowPulse=glowPulse;committedGlowMode=glowMode;committedOutlineColor=outlineColor;committedOutlineThickness=outlineThickness;committedOutlineGlow=outlineGlow;backdropPreview=true;backdropColorValid=true;outlineColorValid=true;
  }
  void EndBackdropPreview(bool accepted) {
   if(!accepted) {background=committedBackground;alpha=committedAlpha;glowPulse=committedGlowPulse;glowMode=committedGlowMode;outlineColor=committedOutlineColor;outlineThickness=committedOutlineThickness;outlineGlow=committedOutlineGlow;}
   backdropPreview=false;UpdateBackground();
  }
  void PlaceEditor(Window editor) {
   editor.WindowStartupLocation=WindowStartupLocation.Manual;
   var area=Program.Area(Left,Top);
   editor.Left=Left+Width+12;
   if(editor.Left+editor.Width>area.Right)editor.Left=Left-editor.Width-12;
   editor.Left=Math.Max(area.Left,Math.Min(area.Right-editor.Width,editor.Left));
   editor.Top=Math.Max(area.Top,Math.Min(area.Bottom-editor.Height,Top));
  }
  static void AddColorPresets(StackPanel parent,TextBox input) {
   var swatches=new WrapPanel {Name=input.Name+"Presets",Margin=new Thickness(0,8,0,8)};parent.Children.Add(swatches);
   foreach(var hex in new[]{"#2C2E37","#24425C","#254B43","#473657","#B8E1E4","#F0D9EB"}) {
    var button=new Button {Background=new SolidColorBrush(ParseColor(hex)),Width=32,Height=28,Margin=new Thickness(0,0,6,0),ToolTip=hex};
    System.Windows.Automation.AutomationProperties.SetName(button,"Выбрать цвет "+hex);
    button.Click+=(s,e)=>input.Text=hex;swatches.Children.Add(button);
   }
  }
  void BackdropEditor(StackPanel parent) {
   parent.Children.Add(new TextBlock {Text="Цвет подложки",Margin=new Thickness(0,12,0,4)});
   var input=new TextBox {Name="BackdropColor",Text=ColorHex(background),ToolTip="Цвет в формате #RRGGBB"};
   parent.Children.Add(input);
   AddColorPresets(parent,input);
   var error=new TextBlock {Text="Введите #RRGGBB",Foreground=Brushes.Salmon,Visibility=Visibility.Collapsed};
   parent.Children.Add(error);
   input.TextChanged+=(s,e)=> {
    try {background=ParseColor(input.Text);UpdateBackground();error.Visibility=Visibility.Collapsed;backdropColorValid=true;}
    catch(FormatException) {error.Visibility=Visibility.Visible;backdropColorValid=false;}
   };
   var label=new TextBlock();parent.Children.Add(label);
   var opacity=new Slider {Name="BackdropTransparency",TickFrequency=1,IsSnapToTickEnabled=true,Minimum=0,Maximum=100,Value=(1-alpha)*100,ToolTip="Прозрачность подложки"};
   parent.Children.Add(opacity);
   Action update=()=> {alpha=1-opacity.Value/100;label.Text="Прозрачность подложки · "+Math.Round(opacity.Value)+"%";UpdateBackground();};
   opacity.ValueChanged+=(s,e)=>update();update();
   var choose=new Button {Content="Палитра Windows…",Margin=new Thickness(0,4,0,8)};parent.Children.Add(choose);
   choose.Click+=(s,e)=> {
    using(var dialog=new Forms.ColorDialog {FullOpen=true,Color=System.Drawing.Color.FromArgb(background.R,background.G,background.B)}) {
     if(dialog.ShowDialog(new DialogOwner(Window.GetWindow(parent)))==Forms.DialogResult.OK)
      input.Text=ColorHex(Color.FromRgb(dialog.Color.R,dialog.Color.G,dialog.Color.B));
    }
   };
  }
  void Settings() {
   BeginBackdropPreview();
   bool accepted=false;
   var w=new Window {Title="Настройки папки — "+name,Width=500,Height=Math.Min(650,Program.Area(Left,Top).Height-40),Owner=this,ShowInTaskbar=false};
   PlaceEditor(w);
   w.Closed+=(s,e)=>EndBackdropPreview(accepted);
   var layout=new SettingsLayout(w);
   var appearance=layout.Page("Вид");
   string chosenTheme=iconTheme;
   AddThemePicker(appearance,value=>chosenTheme=value);
   BackdropEditor(appearance);
   var labelColor=new CheckBox {Content="Тёмный текст названия папки",IsChecked=darkCaption};
   appearance.Children.Add(labelColor);

   var outlineOptions=layout.Page("Обводка");OutlineEditor(outlineOptions);
   var iconOptions=layout.Page("Значки");
   var scaleLabel=new TextBlock();
   iconOptions.Children.Add(scaleLabel);
   var size=new Slider {Name="IconScale",Minimum=50,Maximum=230,TickFrequency=5,IsSnapToTickEnabled=true,Value=iconSize/56.0*100,ToolTip="Масштаб значков"};
   iconOptions.Children.Add(size);
   Action scaleText=()=>scaleLabel.Text="Масштаб · "+Math.Round(size.Value)+"%";
   size.ValueChanged+=(s,e)=>scaleText();scaleText();
   var names=new CheckBox {Content="Показывать названия значков",IsChecked=labels};
   iconOptions.Children.Add(names);
   var motion=new CheckBox {Content="Анимации значков и панелей",IsChecked=animations};
   iconOptions.Children.Add(motion);

   var dimensions=layout.Page("Папка");
   dimensions.Children.Add(new TextBlock {Text="Размер по сетке",FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,12)});
   var fields=new Grid();
   fields.ColumnDefinitions.Add(new ColumnDefinition());
   fields.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(16)});
   fields.ColumnDefinitions.Add(new ColumnDefinition());
   var left=new StackPanel();var right=new StackPanel();fields.Children.Add(left);Grid.SetColumn(right,2);fields.Children.Add(right);dimensions.Children.Add(fields);
   var width=Field(left,"Столбцы · 1–20",columns.ToString());
   var height=Field(right,"Ряды · 1–20",rows.ToString());

   var behavior=dimensions;
   behavior.Children.Add(new Separator {Margin=new Thickness(0,8,0,12)});
   var files=new RadioButton {Content="Перемещать файлы в папку",IsChecked=storeFiles,GroupName="SettingsMode"};
   behavior.Children.Add(files);
   behavior.Children.Add(new RadioButton {Content="Создавать ярлыки на файлы",IsChecked=!storeFiles,GroupName="SettingsMode"});
   var topmost=new CheckBox {Content="Поверх остальных окон",IsChecked=Topmost};
   behavior.Children.Add(topmost);

   var share=new Button {Content="Применить к другим папкам…",Margin=new Thickness(0,0,0,12)};
   layout.Actions.Children.Add(share);
   var save=new Button {Name="SaveFolderSettings",Content="Сохранить"};
   SettingsLayout.Buttons(layout.Actions,w,save);
   save.Click+=(s,e)=> {
    if(!backdropColorValid||!outlineColorValid) {MessageBox.Show(w,"Исправьте HEX-код цвета.");return;}
    int c,r;
    if(!int.TryParse(width.Text,out c)||c<1||c>20||!int.TryParse(height.Text,out r)||r<1||r>20) {
     MessageBox.Show(w,"Число столбцов и рядов должно быть от 1 до 20.");return;
    }
    var previousLayout=CaptureLayout();
    int oldSize=iconSize;bool oldLabels=labels;
    bool layoutChange=c!=columns||r!=rows||(int)Math.Round(size.Value*56/100)!=iconSize||(names.IsChecked==true)!=labels;
    iconSize=Math.Max(28,Math.Min(128,(int)Math.Round(size.Value*56/100)));labels=names.IsChecked==true;
    if(!SetGrid(c,r)) {
     iconSize=oldSize;labels=oldLabels;
     MessageBox.Show(w,Program.Locked?"Снимите закрепление папок в трее, чтобы изменить размер.":"Недостаточно свободного места. Уменьшите размер или переместите соседние папки.");return;
    }
    iconTheme=chosenTheme;darkCaption=labelColor.IsChecked==true;
    storeFiles=files.IsChecked==true;animations=motion.IsChecked==true;Topmost=topmost.IsChecked==true;
    accepted=true;EndBackdropPreview(true);
    RedrawStyle();
    if(layoutChange)Program.Remember(previousLayout);
    w.Close();
   };
   share.Click+=(s,e)=> {save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(accepted)ShareStyle();};
   w.ShowDialog();
  }

  static TextBox Field(StackPanel p,string title,string value) { p.Children.Add(new TextBlock { Text=title, Margin=new Thickness(0,0,0,3) }); var t=new TextBox { Text=value, Margin=new Thickness(0,0,0,10) }; p.Children.Add(t); return t; }
  public string Diagnostics() { return name+" | folder="+folder+" | entries="+Directory.GetFileSystemEntries(folder).Length+" | visibleCells="+icons.Children.Count+" | size="+ActualWidth+"x"+ActualHeight+" | icons="+cache.Count+" | resolved="+cache.Values.Count(v=>v!=null); }
  public string Test() {
   string id="GridNest-test-"+Guid.NewGuid().ToString("N"); string temp=Path.Combine(Program.Home,id); Directory.CreateDirectory(temp);
   string source=Path.Combine(temp,id+".txt"); File.WriteAllText(source,"local test");
   string first=Path.Combine(folder,id+".txt.lnk"), second=Path.Combine(folder,id+".txt (2).lnk"), third=Path.Combine(folder,id+".txt (3).lnk");
   double oldW=Width,oldH=Height;
   var oldOrder=new List<string>(order);
   Color oldBackground=background;
   bool oldMode=storeFiles; int oldSize=iconSize,oldColumns=columns,oldRows=rows; storeFiles=false;
   string dataSource=Path.Combine(temp,id+"-data.bin"),dataTarget=Path.Combine(folder,id+"-data.bin"),dataTarget2=Path.Combine(folder,id+"-data (2).bin"),directoryTarget=Path.Combine(folder,id+"-folder");
   try {
    var errors=AddFiles(new[]{source}); errors.AddRange(AddFiles(new[]{source}));
    string copy=Path.Combine(temp,Path.GetFileName(first)); if(File.Exists(first)) { File.Copy(first,copy); errors.AddRange(AddFiles(new[]{copy})); }
    bool drop=errors.Count==0&&File.Exists(source)&&File.Exists(first)&&File.Exists(second)&&File.Exists(third);
    Refresh();UpdateLayout();bool noFakeScroll=order.Count>columns*rows||scroll.ScrollableHeight<.1;
    SnapSize(257,319); UpdateLayout(); Save(); var x=XElement.Load(config); bool resize=Math.Abs((Width-32)/CellWidth-Math.Round((Width-32)/CellWidth))<.001&&Math.Abs((Height-70)/CellHeight-Math.Round((Height-70)/CellHeight))<.001&&Get(x,"Width",0)==Width&&Get(x,"Rows",0)==rows;
    var scrollBounds=scroll.TransformToAncestor(this).TransformBounds(new Rect(0,0,scroll.ActualWidth,scroll.ActualHeight)); var gripBounds=resizeHandle.TransformToAncestor(this).TransformBounds(new Rect(0,0,resizeHandle.ActualWidth,resizeHandle.ActualHeight)); bool corner=scrollBounds.Bottom-8<=gripBounds.Top+.01&&resizeHandle.ActualHeight<=32;
    bool oldMotion=animations; animations=false; var testScale=new ScaleTransform(1.3,1.3); var testLift=new TranslateTransform(0,-3); IconMotion(testScale,testLift,1.08,-1); Save(); bool reduced=testScale.ScaleX==1&&testLift.Y==0&&(string)XElement.Load(config).Attribute("Animations")=="false"; animations=oldMotion;
    bool icon=ShellIcon.Get(first,128)!=null;
    string renamed=RenamePath(third,id+"-renamed"); bool rename=File.Exists(renamed)&&!File.Exists(third)&&renamed.EndsWith(".lnk"); File.Move(renamed,third);
    string tempFolder=Path.Combine(temp,"Before"); Directory.CreateDirectory(tempFolder); File.WriteAllText(Path.Combine(tempFolder,"marker.txt"),"test"); string after=RenamePath(tempFolder,"After"); bool folderRename=Directory.Exists(after)&&File.Exists(Path.Combine(after,"marker.txt"))&&!Directory.Exists(tempFolder);
    bool conflict=false; try { RenamePath(third,Path.GetFileNameWithoutExtension(first)); } catch(IOException) { conflict=File.Exists(first)&&File.Exists(third); }
    var target=Program.Panels.FirstOrDefault(p=>p!=this); bool moved=false;
    if(target!=null) { string expected=Path.Combine(target.folder,Path.GetFileName(third)); var errs=target.AddFiles(new[]{third},true); moved=errs.Count==0&&!File.Exists(third)&&File.Exists(expected); if(File.Exists(expected))File.Move(expected,third); target.Refresh(); }
    PlaceAt(Path.GetFileName(third),0); LoadOrder(); bool manual=order[0]==Path.GetFileName(third); PlaceAt(Path.GetFileName(first),columns*rows+1); LoadOrder(); manual=manual&&order[columns*rows+1]==Path.GetFileName(first)&&order.Count(n=>n.Equals(Path.GetFileName(first),StringComparison.OrdinalIgnoreCase))==1;
    Refresh(); UpdateLayout(); scroll.ScrollToEnd(); UpdateLayout(); corner=corner&&scroll.ScrollableHeight>0&&Math.Abs(scroll.VerticalOffset-scroll.ScrollableHeight)<1;
    background=ParseColor("#24425C"); UpdateBackground(); Save(); var saved=XElement.Load(config); bool color=(string)saved.Attribute("Background")=="#24425C"&&ColorHex(ParseColor("A1B2C3"))=="#A1B2C3"; try { ParseColor("wrong"); color=false; } catch(FormatException) {}
    storeFiles=true; File.WriteAllText(dataSource,"first"); var fileErrors=AddFiles(new[]{dataSource}); bool originals=fileErrors.Count==0&&!File.Exists(dataSource)&&File.ReadAllText(dataTarget)=="first";
    File.WriteAllText(dataSource,"second"); fileErrors=AddFiles(new[]{dataSource}); originals=originals&&fileErrors.Count==0&&!File.Exists(dataSource)&&File.ReadAllText(dataTarget)=="first"&&File.ReadAllText(dataTarget2)=="second";
    string dirSource=Path.Combine(temp,id+"-folder"); Directory.CreateDirectory(dirSource); File.WriteAllText(Path.Combine(dirSource,"child.txt"),"nested"); fileErrors=AddFiles(new[]{dirSource}); originals=originals&&fileErrors.Count==0&&!Directory.Exists(dirSource)&&File.ReadAllText(Path.Combine(directoryTarget,"child.txt"))=="nested";
    bool recursive=false; try { MoveEntry(directoryTarget,Path.Combine(directoryTarget,"inside")); } catch(IOException) { recursive=Directory.Exists(directoryTarget); }
    int beforeColumns=columns,beforeRows=rows; SetIconSize(84); Save(); bool scaling=iconSize==84&&columns==beforeColumns&&rows==beforeRows&&(string)XElement.Load(config).Attribute("Mode")=="Files";
    bool themed=Application.Current.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] is Style;
    return name+": file-to-lnk, duplicate names, copy existing lnk, preserve source="+(drop?"PASS":"FAIL "+string.Join(";",errors))+"; grid/persist="+(resize?"PASS":"FAIL")+"; shortcut icon="+(icon?"PASS":"FAIL")+"; rename/keep-extension="+(rename?"PASS":"FAIL")+"; folder rename="+(folderRename?"PASS":"FAIL")+"; rename conflict="+(conflict?"PASS":"FAIL")+"; inter-folder move="+(moved?"PASS":"FAIL")+"; manual order/persist="+(manual?"PASS":"FAIL")+"; color/persist/validation="+(color?"PASS":"FAIL")+"; resize-corner-clear="+(corner?"PASS":"FAIL")+"; animation-disable/persist="+(reduced?"PASS":"FAIL")+"; original-files-move/conflicts="+(originals&&recursive?"PASS":"FAIL")+"; icon-scale/persist="+(scaling?"PASS":"FAIL")+"; themed-scroll="+(themed?"PASS":"FAIL")+"; no-empty-grid-scroll="+(noFakeScroll?"PASS":"FAIL");
   } catch(Exception e) { return name+": FAIL "+e.ToString(); }
   finally { storeFiles=oldMode; iconSize=oldSize; SetGrid(oldColumns,oldRows); background=oldBackground; UpdateBackground(); Save(); foreach(string p in new[]{first,second,third,dataTarget,dataTarget2}) if(File.Exists(p))File.Delete(p); if(Directory.Exists(directoryTarget)&&Path.GetFullPath(directoryTarget).StartsWith(Path.GetFullPath(folder)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(directoryTarget)==id+"-folder")Directory.Delete(directoryTarget,true); if(Directory.Exists(temp))Directory.Delete(temp,true); order=oldOrder; SaveOrder(); cache.Clear(); Refresh(); UpdateLayout(); }
  }
 }
 public static class ShellIcon {
  [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx,cy; }
  [ComImport,Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IShellItemImageFactory { [PreserveSig] int GetImage(SIZE size,uint flags,out IntPtr bitmap); }
  [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=true)] static extern int SHCreateItemFromParsingName(string path,IntPtr context,ref Guid iid,[MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);
  [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
  public static ImageSource Get(string path,int size) {
   IShellItemImageFactory factory=null; IntPtr bitmap=IntPtr.Zero;
   try { Guid id=new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"); if(SHCreateItemFromParsingName(path,IntPtr.Zero,ref id,out factory)!=0)return null; if(factory.GetImage(new SIZE { cx=size,cy=size },0x4|0x1,out bitmap)!=0)return null; var result=Imaging.CreateBitmapSourceFromHBitmap(bitmap,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions()); result.Freeze(); return result; }
   catch { return null; } finally { if(bitmap!=IntPtr.Zero)DeleteObject(bitmap); if(factory!=null)Marshal.ReleaseComObject(factory); }
  }
 }
}


