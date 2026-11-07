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
[assembly: AssemblyVersion("0.8.0.0")]
[assembly: AssemblyFileVersion("0.8.0.0")]

namespace GridNest {
 public class Program {
  public static string Home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GridNest", ".app");
  public static string Data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GridNest");
  public static List<PanelWindow> Panels = new List<PanelWindow>();
  static System.Threading.Mutex mutex;
  static Forms.NotifyIcon tray;
  static System.Threading.EventWaitHandle showEvent;
  static System.Threading.RegisteredWaitHandle showWait;
  static Window searchWindow;
  public const int PanelGrid=16;
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
   try { string mode=(string)XElement.Load(Path.Combine(Home,"app-settings.xml")).Attribute("Theme"); if(new[]{"Dark","Light","System"}.Contains(mode))ThemeMode=mode; } catch {}
   ApplyAppearance();
   EventManager.RegisterClassHandler(typeof(Window),FrameworkElement.LoadedEvent,new RoutedEventHandler((s,e)=> {var window=s as Window;if(window!=null&&e.OriginalSource==window)PaintCaption(window);}));
   Microsoft.Win32.SystemEvents.UserPreferenceChanged+=(s,e)=> { if(ThemeMode=="System")app.Dispatcher.BeginInvoke(new Action(ApplyAppearance)); };
   showEvent=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.AutoReset,"Local\\GridNestShow");
   showWait=System.Threading.ThreadPool.RegisterWaitForSingleObject(showEvent,(s,t)=>app.Dispatcher.BeginInvoke(new Action(ShowPanels)),null,-1,false);
   string[] preferred={"Работа","AI","Сеть","Разработка","3D и CAD","Медиа","Игры","VR","Браузеры","Система"};
   string[] names=preferred.Where(n=>Directory.Exists(Path.Combine(Data,n))).Concat(Directory.GetDirectories(Data).Select(Path.GetFileName).Where(n=>!n.StartsWith(".")&&!preferred.Contains(n))).ToArray();
   if(names.Length==0&&!File.Exists(Path.Combine(Home,"initialized")))names=new[]{"Работа","AI","Сеть"};
   for(int i=0;i<names.Length;i++) { PanelWindow p=new PanelWindow(names[i],i); Panels.Add(p); p.Show(); }
   if(!File.Exists(Path.Combine(Home,"initialized")))File.WriteAllText(Path.Combine(Home,"initialized"),"1");
   if(args.Contains("--arrange"))ArrangePanels();
   SetupTray();
   app.Exit += (s,e) => { foreach(var p in Panels) p.Save(); if(tray!=null) { tray.Visible=false; tray.Dispose(); } if(showWait!=null)showWait.Unregister(null); if(showEvent!=null)showEvent.Dispose(); };
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
   var folders=new Forms.ToolStripMenuItem("Папки");
   folders.DropDownOpening+=(s,e)=> { folders.DropDownItems.Clear(); foreach(var p in Panels) { var entry=new Forms.ToolStripMenuItem(p.FolderName) { Checked=p.IsVisible }; entry.Click+=(x,y)=>Application.Current.Dispatcher.BeginInvoke(new Action(()=> { if(p.IsVisible)p.Hide();else { p.Show();p.Activate(); } })); folders.DropDownItems.Add(entry); } PaintTrayItems(folders.DropDownItems); }; menu.Items.Add(folders);
   add("Настройки приложения…",AppSettings);
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
   var w=new Window { Title="Настройки приложения — GridNest", Width=410, Height=340, ResizeMode=ResizeMode.NoResize, WindowStartupLocation=WindowStartupLocation.CenterScreen };
   var sp=new StackPanel { Margin=new Thickness(26) };w.Content=sp;
   sp.Children.Add(new TextBlock { Text="Оформление GridNest",FontSize=23,Margin=new Thickness(0,0,0,16) });
   var dark=new RadioButton {Content="Тёмная тема",GroupName="AppTheme",IsChecked=ThemeMode=="Dark"};var light=new RadioButton {Content="Светлая тема",GroupName="AppTheme",IsChecked=ThemeMode=="Light"};var system=new RadioButton {Content="Как в Windows",GroupName="AppTheme",IsChecked=ThemeMode=="System"};sp.Children.Add(dark);sp.Children.Add(light);sp.Children.Add(system);
   sp.Children.Add(new TextBlock {Text="Тема меняет меню и окна настроек. Цвета папок сохраняются.",TextWrapping=TextWrapping.Wrap,FontSize=12,Margin=new Thickness(0,12,0,12)});
   var apply=new Button {Content="Применить",IsDefault=true};sp.Children.Add(apply);apply.Click+=(s,e)=> {SelectTheme(light.IsChecked==true?"Light":system.IsChecked==true?"System":"Dark");w.Close();};w.ShowDialog();
  }
  public static void SelectTheme(string mode) { if(!new[]{"Dark","Light","System"}.Contains(mode))throw new ArgumentException("Unknown theme");ThemeMode=mode;new XElement("Application",new XAttribute("Theme",ThemeMode)).Save(Path.Combine(Home,"app-settings.xml"));ApplyAppearance(); }
  public static void ShowPanels() { foreach(var p in Panels) { p.Show(); p.WindowState=WindowState.Normal; } if(Panels.Count>0)Panels[0].Activate(); }
  public static void HidePanels() { foreach(var p in Panels)p.Hide(); if(searchWindow!=null)searchWindow.Hide(); }
  public static void Prompt(string title,string initial,Action<string> apply) {
   var w=new Window { Title=title, Width=380, Height=160, ResizeMode=ResizeMode.NoResize, WindowStartupLocation=WindowStartupLocation.CenterScreen };
   var sp=new StackPanel { Margin=new Thickness(16) }; w.Content=sp;
   var input=new TextBox { Text=initial, Margin=new Thickness(0,0,0,14) }; sp.Children.Add(input);
   var ok=new Button { Content="Создать", IsDefault=true, Padding=new Thickness(10,6,10,6) }; sp.Children.Add(ok);
   ok.Click+=(s,e)=> { try { apply(input.Text); w.Close(); } catch(Exception ex) { MessageBox.Show(w,ex.Message,"Не удалось создать папку"); } };
   w.Loaded+=(s,e)=> { input.Focus(); input.SelectAll(); }; w.ShowDialog();
  }
  public static PanelWindow NewPanel(string name,bool files=true) {
   string target=PanelWindow.ValidateFolderName(name);
   if(Directory.Exists(target)||File.Exists(target))throw new IOException("Папка с таким именем уже существует.");
   Directory.CreateDirectory(target);
   var panel=new PanelWindow(name,Panels.Count); panel.storeFiles=files; Panels.Add(panel); panel.Show();
   var a=SystemParameters.WorkArea; bool placed=false;
   for(double y=a.Top+16;y+panel.Height<=a.Bottom&&!placed;y+=PanelGrid)for(double x=a.Right-panel.Width-16;x>=a.Left;x-=PanelGrid) {
    var rect=new Rect(x,y,panel.Width+16,panel.Height+16);
    if(Panels.Where(p=>p!=panel).All(p=>!rect.IntersectsWith(new Rect(p.Left,p.Top,p.Width+16,p.Height+16)))) { panel.Left=x; panel.Top=y; placed=true; break; }
   }
   panel.AlignPosition(); panel.Save(); return panel;
  }
  public static void CreateFolder() {
   var w=new Window { Title="Новая папка — GridNest", Width=440, Height=340, ResizeMode=ResizeMode.NoResize, WindowStartupLocation=WindowStartupLocation.CenterScreen };
   var sp=new StackPanel { Margin=new Thickness(24) }; w.Content=sp;
   sp.Children.Add(new TextBlock { Text="Создать пространство", FontSize=23, Margin=new Thickness(0,0,0,16) });
   var input=new TextBox { Text="Новая папка", Margin=new Thickness(0,0,0,12) }; sp.Children.Add(input);
   var files=new RadioButton { Content="Файлы — перемещать оригиналы внутрь", IsChecked=true, GroupName="Mode" }; sp.Children.Add(files);
   sp.Children.Add(new RadioButton { Content="Ярлыки — создавать ссылки на файлы", GroupName="Mode" });
   var create=new Button { Content="Создать папку", IsDefault=true, Margin=new Thickness(0,18,0,0) }; sp.Children.Add(create);
   create.Click+=(s,e)=> { try { NewPanel(input.Text,files.IsChecked==true); w.Close(); } catch(Exception ex) { MessageBox.Show(w,ex.Message,"Создание папки"); } }; w.Loaded+=(s,e)=> { input.Focus();input.SelectAll(); }; w.ShowDialog();
  }
  public static void OpenArchive() { var p=Path.Combine(Data,".archive"); Directory.CreateDirectory(p); PanelWindow.OpenPath(p); }
  public static void ArrangePanels() {
   if(Panels.Count==0)return;
   var area=SystemParameters.WorkArea; double width=Panels.Max(p=>p.Width), step=Math.Ceiling((width+16)/PanelGrid)*PanelGrid;
   int maxCols=Math.Max(1,(int)Math.Floor((area.Width-32)/step)); int primary=Math.Min(3,maxCols);
   double start=area.Left+Math.Floor((area.Width-16-primary*step)/PanelGrid)*PanelGrid;
   var bottoms=Enumerable.Repeat(area.Top+16,maxCols).ToArray(); int missed=0;
   foreach(var p in Panels) {
    int best=-1; double bestY=double.MaxValue;
    for(int c=0;c<maxCols;c++) { double x=c<primary?start+c*step:start-(c-primary+1)*step; double y=bottoms[c]; if(x>=area.Left&&x+p.Width<=area.Right&&y+p.Height<=area.Bottom&&(best<0||c<primary&&best>=primary||((c<primary)==(best<primary)&&y<bestY))) { best=c; bestY=y; } }
    if(best<0) { missed++; continue; }
    p.Left=best<primary?start+best*step:start-(best-primary+1)*step; p.Top=bestY; p.Save(); bottoms[best]=area.Top+Math.Ceiling((bestY-area.Top+p.Height+16)/PanelGrid)*PanelGrid;
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
    return "FEATURES: create="+(create?"PASS":"FAIL")+"; search="+(search?"PASS":"FAIL")+"; panel grid="+(grid?"PASS":"FAIL")+"; show/hide="+(visibility?"PASS":"FAIL")+"; archive without data loss="+(remove?"PASS":"FAIL")+"; tray="+(tray!=null&&tray.Visible?"PASS":"FAIL")+"; tray-only="+(Panels.All(z=>!z.ShowInTaskbar)?"PASS":"FAIL")+"; themes/persist="+(themes?"PASS":"FAIL");
   } catch(Exception e) { return "FEATURES: FAIL "+e; }
   finally { SelectTheme(oldTheme);if(p!=null)archive=p.Archive(); if(archive!=null) { string parent=Path.GetDirectoryName(archive); if(Path.GetFullPath(parent).StartsWith(Path.GetFullPath(Path.Combine(Data,".archive"))+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(archive)==name)Directory.Delete(parent,true); } string config=Path.Combine(Home,name+".xml"); if(File.Exists(config))File.Delete(config); }
  }
 }
 public class PanelWindow : Window {
  string name, folder, config; Border card; WrapPanel icons; ScrollViewer scroll; Grid frame; TextBlock caption; MenuItem animationMenu;
  const string InternalDrag="GridNest.InternalFile";
  int columns=3, rows=3; bool snapping;
  bool retired; bool animations=true; public bool storeFiles=true; FrameworkElement resizeHandle; List<string> order=new List<string>();
  string OrderFile { get { return Path.Combine(folder,".gridnest-order.xml"); } }
  public string FolderName { get { return name; } }
  public string FolderPath { get { return folder; } }
  double CellWidth { get { return iconSize+16; } }
  double CellHeight { get { return iconSize+(labels?30:10); } }
  DispatcherTimer debounce; FileSystemWatcher watcher; bool loaded, labels=false; int iconSize=56; double alpha=0.84;
  Color background=Color.FromRgb(44,46,55);
  Dictionary<string,ImageSource> cache=new Dictionary<string,ImageSource>(StringComparer.OrdinalIgnoreCase);
  public PanelWindow(string n,int index) {
   name=n; folder=Path.Combine(Program.Data,n); config=Path.Combine(Program.Home,n+".xml"); Directory.CreateDirectory(folder);
   Title="GridNest — "+name; Width=266; Height=264; Left=30+index*290; Top=90;
   using(var stream=typeof(Program).Assembly.GetManifestResourceStream("GridNest.AppIcon.ico")) { if(stream!=null)Icon=BitmapFrame.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad); }
   MinWidth=1; MinHeight=1; WindowStyle=WindowStyle.None; AllowsTransparency=true; Background=Brushes.Transparent;
   ShowInTaskbar=false; ResizeMode=ResizeMode.NoResize;
   Load();
   frame=new Grid(); frame.RowDefinitions.Add(new RowDefinition()); frame.RowDefinitions.Add(new RowDefinition { Height=new GridLength(38) }); Content=frame;
   card=new Border { CornerRadius=new CornerRadius(30), Padding=new Thickness(14), AllowDrop=true }; UpdateBackground();
   frame.Children.Add(card);
   Grid content=new Grid(); card.Child=content;
   content.RowDefinitions.Add(new RowDefinition());
   content.RowDefinitions.Add(new RowDefinition { Height=new GridLength(12) });
   icons=new WrapPanel { Orientation=Orientation.Horizontal, VerticalAlignment=VerticalAlignment.Top };
   scroll=new ScrollViewer { Content=icons, HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled, VerticalScrollBarVisibility=ScrollBarVisibility.Auto, PanningMode=PanningMode.VerticalOnly };
   content.Children.Add(scroll);
   caption=new TextBlock { Text=name, FontFamily=new FontFamily("Segoe UI"), FontSize=19, Foreground=Brushes.White, HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center };
   caption.Effect=new System.Windows.Media.Effects.DropShadowEffect { BlurRadius=5, ShadowDepth=1, Opacity=.8 };
   Grid.SetRow(caption,1); frame.Children.Add(caption);
   // An explicit resize handle works with transparent borderless windows.
   System.Windows.Controls.Primitives.Thumb grip=new System.Windows.Controls.Primitives.Thumb { Width=16, Height=12, HorizontalAlignment=HorizontalAlignment.Right, VerticalAlignment=VerticalAlignment.Bottom, Cursor=Cursors.SizeNWSE, Opacity=.65, ToolTip="Изменить размер папки" };
   grip.SetResourceReference(Control.ForegroundProperty,"PanelHandle");
   resizeHandle=grip; Grid.SetRow(grip,1); grip.Template=GripTemplate(); content.Children.Add(grip);
   var moveDots=new Canvas {Width=12,Height=10,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
   for(int y=0;y<3;y++)for(int x=0;x<2;x++) {var dot=new System.Windows.Shapes.Ellipse {Width=2,Height=2};dot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty,"PanelHandle");Canvas.SetLeft(dot,3+x*4);Canvas.SetTop(dot,1+y*3);moveDots.Children.Add(dot);}
   var moveHandle=new Border {Child=moveDots,Width=24,Height=12,Background=Brushes.Transparent,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Cursor=Cursors.SizeAll,ToolTip="Перетащить папку"};
   Grid.SetRow(moveHandle,1); content.Children.Add(moveHandle);
   moveHandle.MouseLeftButtonDown+=(s,e)=> { try { DragMove(); AlignPosition(); Save(); } catch {} e.Handled=true; };
   grip.MouseEnter+=(s,e)=>Fade(grip,.9,120); grip.MouseLeave+=(s,e)=>Fade(grip,.4,120);
   double dragW=0,dragH=0; Point dragOrigin=new Point();
   grip.DragStarted+=(s,e)=> { dragW=Width; dragH=Height; dragOrigin=Mouse.GetPosition(this); };
   grip.DragDelta+=(s,e)=> { Point now=Mouse.GetPosition(this); SnapSize(dragW+now.X-dragOrigin.X,dragH+now.Y-dragOrigin.Y); };
   grip.DragCompleted+=(s,e)=> { AlignPosition(); Save(); };
   card.MouseLeftButtonDown+=(s,e)=> { if(e.OriginalSource==card || e.OriginalSource==icons || e.OriginalSource==content) { try { DragMove(); AlignPosition(); Save(); } catch {} } };
   caption.MouseLeftButtonDown+=(s,e)=> { try { DragMove(); AlignPosition(); Save(); } catch {} };
   card.PreviewDragOver+=(s,e)=> { e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?(storeFiles||e.Data.GetDataPresent(InternalDrag)||((string[])e.Data.GetData(DataFormats.FileDrop)).All(p=>IsDesktopItem(p)&&IsShortcut(p))?DragDropEffects.Move:DragDropEffects.Copy):DragDropEffects.None; e.Handled=true; };
   card.Drop+=DropFiles;
   ContextMenu menu=new ContextMenu();
   Add(menu,"Открыть папку — добавить ярлыки",()=>Open(folder));
   Add(menu,"Обновить значки",()=> { cache.Clear(); Refresh(); }); menu.Items.Add(new Separator());
   Add(menu,"Размер и оформление…",Settings);
   Add(menu,"Цвет подложки…",ColorSettings);
   Add(menu,"Настройки приложения…",Program.AppSettings);
   var motion=new MenuItem { Header="Анимации значков и панелей", IsCheckable=true, IsChecked=animations };
   animationMenu=motion;
   motion.Click+=(s,e)=> { animations=motion.IsChecked; Refresh(); Save(); }; menu.Items.Add(motion);
   Add(menu,"Переименовать папку…",RenameFolder);
   Add(menu,"Создать папку…",Program.CreateFolder);
   Add(menu,"Убрать папку в архив…",()=> { if(MessageBox.Show("Убрать папку «"+name+"» с рабочего стола?\nВсё содержимое сохранится в архиве GridNest\\.archive.","Убрать папку",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes) { try { Archive(); } catch(Exception ex) { MessageBox.Show(ex.Message,"Не удалось убрать папку"); } } });
   Add(menu,"Поиск по всем папкам… (Ctrl+F)",Program.Search);
   Add(menu,"Выровнять все папки по сетке",()=>Program.ArrangePanels());
   Add(menu,"Показывать названия значков",()=> { labels=!labels; SetGrid(columns,rows); Refresh(); Save(); });
   Add(menu,"Поверх остальных окон",()=> { Topmost=!Topmost; Save(); });
   Add(menu,"Помощь",()=>Open(Path.Combine(Program.Home,"README.txt")));
   menu.Items.Add(new Separator());
   Add(menu,"Закрыть все панели",()=>Application.Current.Shutdown());
   frame.ContextMenu=menu;
   PreviewMouseWheel+=(s,e)=> { if((Keyboard.Modifiers&ModifierKeys.Control)!=0) { SetIconSize(iconSize+(e.Delta>0?8:-8)); e.Handled=true; } };
   PreviewKeyDown+=(s,e)=> { if(e.Key==Key.F&&(Keyboard.Modifiers&ModifierKeys.Control)!=0) { Program.Search(); e.Handled=true; } };
   debounce=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(450) }; debounce.Tick+=(s,e)=> { debounce.Stop(); cache.Clear(); Refresh(); };
   watcher=new FileSystemWatcher(folder) { NotifyFilter=NotifyFilters.FileName|NotifyFilters.DirectoryName|NotifyFilters.LastWrite, EnableRaisingEvents=true };
   FileSystemEventHandler changed=(s,e)=>Dispatcher.BeginInvoke(new Action(()=> { debounce.Stop(); debounce.Start(); }));
   watcher.Created+=changed; watcher.Deleted+=changed; watcher.Changed+=changed; watcher.Renamed+=(s,e)=>changed(s,e);
   Loaded+=(s,e)=> { loaded=true; LoadOrder(); SetGrid(columns,rows); Clamp(); AlignPosition(); Refresh(); Save(); Reveal(); };
   IsVisibleChanged+=(s,e)=> { if(IsVisible&&loaded)Reveal(); };
   SizeChanged+=(s,e)=> { if(loaded&&!snapping) { SnapSize(Width,Height); Save(); } };
   Closed+=(s,e)=> { Save(); watcher.Dispose(); debounce.Stop(); };
  }
  static ControlTemplate GripTemplate() {
   return (ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Thumb'><Grid Background='Transparent'><Path Data='M 4,10 L 11,3 M 9,10 L 14,5' Stroke='{TemplateBinding Foreground}' StrokeThickness='1.6' StrokeStartLineCap='Round' StrokeEndLineCap='Round' HorizontalAlignment='Center' VerticalAlignment='Center'/></Grid></ControlTemplate>");
  }
  static void Add(ContextMenu m,string text,Action action) { var item=new MenuItem { Header=text }; item.Click+=(s,e)=>action(); m.Items.Add(item); }
  void Load() {
   try { var x=XElement.Load(config); Width=Get(x,"Width",Width); Height=Get(x,"Height",Height); Left=Get(x,"Left",Left); Top=Get(x,"Top",Top); iconSize=(int)Math.Max(28,Math.Min(128,Get(x,"IconSize",64))); alpha=Math.Max(.15,Math.Min(1,Get(x,"Opacity",.84))); labels=(string)x.Attribute("Labels")=="true"; Topmost=(string)x.Attribute("Topmost")=="true"; columns=(int)Get(x,"Columns",Math.Round((Width-50)/CellWidth)); rows=(int)Get(x,"Rows",Math.Round((Height-66)/CellHeight)); } catch {}
   try { var x=XElement.Load(config); if(x.Attribute("Background")!=null)background=ParseColor((string)x.Attribute("Background")); } catch {}
   try { animations=(string)XElement.Load(config).Attribute("Animations")!="false"; } catch {}
   try { storeFiles=(string)XElement.Load(config).Attribute("Mode")!="Shortcuts"; } catch {}
   Width=Math.Max(MinWidth,Width); Height=Math.Max(MinHeight,Height);
  }
  static double Get(XElement e,string key,double d) { double v; return double.TryParse((string)e.Attribute(key),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out v)&&!double.IsNaN(v)&&!double.IsInfinity(v)?v:d; }
  void SetGrid(int c,int r) {
   var a=SystemParameters.WorkArea;
   columns=Math.Max(1,Math.Min(c,Math.Max(1,(int)Math.Floor((a.Width-50)/CellWidth))));
   rows=Math.Max(1,Math.Min(r,Math.Max(1,(int)Math.Floor((a.Height-78)/CellHeight))));
   snapping=true;
   try { Width=50+columns*CellWidth; Height=78+rows*CellHeight; if(icons!=null)icons.Width=columns*CellWidth; }
   finally { snapping=false; }
  }
  void SnapSize(double w,double h) { SetGrid((int)Math.Round((w-50)/CellWidth),(int)Math.Round((h-78)/CellHeight)); }
  void Clamp() { var a=SystemParameters.WorkArea; SetGrid(columns,rows); Left=Math.Max(a.Left,Math.Min(Left,a.Right-Width)); Top=Math.Max(a.Top,Math.Min(Top,a.Bottom-Height)); }
  public void AlignPosition() {
   var a=SystemParameters.WorkArea; double maxX=Math.Max(0,Math.Floor((a.Width-Width)/Program.PanelGrid)),maxY=Math.Max(0,Math.Floor((a.Height-Height)/Program.PanelGrid));
   Left=a.Left+Math.Max(0,Math.Min(maxX,Math.Round((Left-a.Left)/Program.PanelGrid)))*Program.PanelGrid;
   Top=a.Top+Math.Max(0,Math.Min(maxY,Math.Round((Top-a.Top)/Program.PanelGrid)))*Program.PanelGrid;
  }
  public void Save() {
   if(!loaded||retired)return;
   try { new XElement("Panel",new XAttribute("Mode",storeFiles?"Files":"Shortcuts"),new XAttribute("Columns",columns),new XAttribute("Rows",rows),new XAttribute("Width",Width),new XAttribute("Height",Height),new XAttribute("Left",Left),new XAttribute("Top",Top),new XAttribute("IconSize",iconSize),new XAttribute("Opacity",alpha),new XAttribute("Background",ColorHex(background)),new XAttribute("Animations",animations?"true":"false"),new XAttribute("Labels",labels?"true":"false"),new XAttribute("Topmost",Topmost?"true":"false")).Save(config); } catch {}
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
   index=Math.Max(0,Math.Min(4095,index)); int previous=order.FindIndex(n=>n.Equals(fileName,StringComparison.OrdinalIgnoreCase));
   while(order.Count<=index)order.Add("");
   string displaced=order[index]; order[index]=fileName;
   if(previous>=0&&previous!=index)order[previous]=displaced;
   else if(previous<0&&!string.IsNullOrEmpty(displaced)) { int hole=order.FindIndex(string.IsNullOrEmpty); if(hole<0)order.Add(displaced); else order[hole]=displaced; }
   SaveOrder();
  }
  void Refresh() {
   if(retired)return;
   icons.Children.Clear();
   string[] files;
   try { files=FileEntries(); SyncOrder(files); } catch { return; }
   int count=Math.Max(columns*rows,order.Count);
   for(int slot=0;slot<count;slot++) {
    string item=slot<order.Count?order[slot]:"";
    if(string.IsNullOrEmpty(item)) { var empty=new Border { Width=CellWidth, Height=CellHeight, Background=Brushes.Transparent, ToolTip="Свободная ячейка" }; icons.Children.Add(empty); continue; }
    string path=Path.Combine(folder,item);
    StackPanel cell=new StackPanel { Width=CellWidth, Height=CellHeight, Background=Brushes.Transparent, ToolTip=Path.GetFileName(path), Cursor=Cursors.Hand, Focusable=true };
    ImageSource source; if(!cache.TryGetValue(path,out source)) { source=ShellIcon.Get(path,iconSize*2); cache[path]=source; }
    var image=new Image { Source=source, Width=iconSize, Height=iconSize, Margin=new Thickness(0,4,0,0), Stretch=Stretch.Uniform, HorizontalAlignment=HorizontalAlignment.Center, RenderTransformOrigin=new Point(.5,.5) };
    var scale=new ScaleTransform(1,1); var lift=new TranslateTransform(); var transform=new TransformGroup(); transform.Children.Add(scale); transform.Children.Add(lift); image.RenderTransform=transform;
    cell.MouseEnter+=(s,e)=>IconMotion(scale,lift,1.08,-1); cell.MouseLeave+=(s,e)=>IconMotion(scale,lift,1,0);
    RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality); cell.Children.Add(image);
    if(labels) cell.Children.Add(new TextBlock { Text=IsShortcut(path)?Path.GetFileNameWithoutExtension(path):Path.GetFileName(path), FontSize=11, Foreground=Brushes.White, TextAlignment=TextAlignment.Center, TextTrimming=TextTrimming.CharacterEllipsis, Margin=new Thickness(0,4,0,0) });
    Point start=new Point(); bool pressed=false,dragged=false;
    cell.PreviewMouseLeftButtonDown+=(s,e)=> { start=e.GetPosition(this); pressed=true; dragged=false; cell.Focus(); cell.CaptureMouse(); IconMotion(scale,lift,.94,1); e.Handled=true; };
    cell.MouseMove+=(s,e)=> {
     if(!pressed||e.LeftButton!=MouseButtonState.Pressed)return;
     Point now=e.GetPosition(this);
     if(Math.Abs(now.X-start.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(now.Y-start.Y)<SystemParameters.MinimumVerticalDragDistance)return;
     pressed=false; dragged=true; cell.ReleaseMouseCapture(); IconMotion(scale,lift,1,0);
     var data=new DataObject(DataFormats.FileDrop,new[]{path}); data.SetData(InternalDrag,path);
     try { DragDrop.DoDragDrop(cell,data,DragDropEffects.Move|DragDropEffects.Copy); } finally { foreach(var p in Program.Panels) { p.cache.Clear(); p.Refresh(); } }
     e.Handled=true;
    };
    cell.MouseLeftButtonUp+=(s,e)=> { bool click=pressed&&!dragged; pressed=false; cell.ReleaseMouseCapture(); IconMotion(scale,lift,cell.IsMouseOver?1.08:1,cell.IsMouseOver?-1:0); if(click)Open(path); e.Handled=true; };
    cell.LostMouseCapture+=(s,e)=> { pressed=false; IconMotion(scale,lift,1,0); };
    cell.KeyDown+=(s,e)=> { if(e.Key==Key.F2) { RenameItem(path); e.Handled=true; } };
    var menu=new ContextMenu(); Add(menu,"Открыть",()=>Open(path)); Add(menu,"Переименовать… (F2)",()=>RenameItem(path)); Add(menu,"Показать в папке",()=>Process.Start("explorer.exe","/select,\""+path+"\"")); cell.ContextMenu=menu;
    icons.Children.Add(cell);
   }
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
   foreach(var source in files) {
    try {
     if(Path.GetFullPath(Path.GetDirectoryName(source)).TrimEnd('\\').Equals(Path.GetFullPath(folder).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)) { if(move&&position>=0) { PlaceAt(Path.GetFileName(source),position); position++; } continue; }
     string ext=Path.GetExtension(source); bool shortcut=File.Exists(source)&&(ext.Equals(".lnk",StringComparison.OrdinalIgnoreCase)||ext.Equals(".url",StringComparison.OrdinalIgnoreCase));
     string dest=Unique(Path.Combine(folder,shortcut?Path.GetFileName(source):Path.GetFileName(source)+".lnk"));
     bool owned=Program.Panels.Any(p=>Path.GetFullPath(Path.GetDirectoryName(source)).TrimEnd('\\').Equals(Path.GetFullPath(p.folder).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase));
     bool desktop=IsDesktopItem(source);
     if(move&&owned||storeFiles||desktop&&shortcut) { dest=Unique(Path.Combine(folder,Path.GetFileName(source))); MoveEntry(source,dest); }
     else if(shortcut)File.Copy(source,dest,false);
     else { Type t=Type.GetTypeFromProgID("WScript.Shell"); object shell=Activator.CreateInstance(t); object link=null; try { link=t.InvokeMember("CreateShortcut",System.Reflection.BindingFlags.InvokeMethod,null,shell,new object[]{dest}); link.GetType().InvokeMember("TargetPath",System.Reflection.BindingFlags.SetProperty,null,link,new object[]{source}); link.GetType().InvokeMember("Save",System.Reflection.BindingFlags.InvokeMethod,null,link,null); } finally { if(link!=null)Marshal.FinalReleaseComObject(link); Marshal.FinalReleaseComObject(shell); } }
     if(position>=0) { PlaceAt(Path.GetFileName(dest),position); position++; }
    } catch(Exception ex) { errors.Add(Path.GetFileName(source)+": "+ex.Message); }
   }
   cache.Clear(); Refresh();
   SaveOrder();
   return errors;
  }
  static bool IsDesktopItem(string path) { string parent=Path.GetFullPath(Path.GetDirectoryName(path)).TrimEnd('\\'); return new[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)}.Any(p=>parent.Equals(Path.GetFullPath(p).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)); }
  static bool IsShortcut(string path) { string ext=Path.GetExtension(path); return ext.Equals(".lnk",StringComparison.OrdinalIgnoreCase)||ext.Equals(".url",StringComparison.OrdinalIgnoreCase); }
  static void MoveEntry(string source,string dest) {
   string s=Path.GetFullPath(source).TrimEnd('\\'),d=Path.GetFullPath(dest).TrimEnd('\\');
   if(Directory.Exists(source)) {
    if(d.StartsWith(s+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Нельзя переместить папку внутрь самой себя.");
    Directory.Move(source,dest);
   } else File.Move(source,dest);
  }
  void SetIconSize(int pixels) { iconSize=Math.Max(28,Math.Min(128,pixels)); SetGrid(columns,rows); Clamp(); AlignPosition(); cache.Clear(); Refresh(); Save(); }
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
   var w=new Window { Title=title, Width=380, Height=160, ResizeMode=ResizeMode.NoResize, WindowStartupLocation=WindowStartupLocation.CenterOwner, Owner=this, ShowInTaskbar=false };
   var sp=new StackPanel { Margin=new Thickness(16) }; w.Content=sp;
   var input=new TextBox { Text=value, Margin=new Thickness(0,0,0,14) }; sp.Children.Add(input);
   var ok=new Button { Content="Переименовать", IsDefault=true, Padding=new Thickness(10,6,10,6) }; sp.Children.Add(ok);
   ok.Click+=(s,e)=> { try { apply(input.Text); w.Close(); } catch(Exception ex) { MessageBox.Show(w,ex.Message,"Не удалось переименовать",MessageBoxButton.OK,MessageBoxImage.Information); } };
   w.Loaded+=(s,e)=> { input.Focus(); input.SelectAll(); }; w.ShowDialog();
  }
  void RenameItem(string path) { AskName("Переименовать значок",Directory.Exists(path)?Path.GetFileName(path):Path.GetFileNameWithoutExtension(path),value=> { string dest=RenamePath(path,value); for(int i=0;i<order.Count;i++)if(order[i].Equals(Path.GetFileName(path),StringComparison.OrdinalIgnoreCase))order[i]=Path.GetFileName(dest); SaveOrder(); cache.Clear(); Refresh(); }); }
  public static string ValidateFolderName(string value) { if(value.StartsWith("."))throw new IOException("Имя папки не должно начинаться с точки."); return RenamedPath(Path.Combine(Program.Data,"Folder"),value); }
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
  void UpdateBackground() { if(card!=null)card.Background=new SolidColorBrush(Color.FromArgb((byte)(alpha*255),background.R,background.G,background.B));double luma=.2126*background.R+.7152*background.G+.0722*background.B;Resources["PanelHandle"]=new SolidColorBrush(luma>150?Color.FromRgb(39,87,97):Color.FromRgb(167,214,213)); }
  sealed class DialogOwner : Forms.IWin32Window { public IntPtr Handle { get; private set; } public DialogOwner(Window w) { Handle=new WindowInteropHelper(w).Handle; } }
  void ColorSettings() {
   var w=new Window { Title="Цвет подложки — "+name, Width=420, Height=400, ResizeMode=ResizeMode.NoResize, Owner=this, WindowStartupLocation=WindowStartupLocation.CenterOwner, ShowInTaskbar=false };
   var stack=new StackPanel { Margin=new Thickness(20) }; w.Content=stack;
   stack.Children.Add(new TextBlock { Text="Цвет для папки «"+name+"»", FontSize=16, Margin=new Thickness(0,0,0,14) });
   var swatches=new WrapPanel { Margin=new Thickness(0,0,0,14) }; stack.Children.Add(swatches);
   var input=new TextBox { Text=ColorHex(background), FontSize=16, Margin=new Thickness(0,0,0,10) };
   foreach(var hex in new[]{"#2C2E37","#24425C","#254B43","#473657","#5B3A3A","#57462B","#172634","#526079"}) {
    var button=new Button { Background=new SolidColorBrush(ParseColor(hex)), Width=32, Height=32, Margin=new Thickness(0,0,7,0), ToolTip=hex }; System.Windows.Automation.AutomationProperties.SetName(button,"Цвет "+hex); button.Click+=(s,e)=>input.Text=hex; swatches.Children.Add(button);
   }
   stack.Children.Add(new TextBlock { Text="HEX-код (#RRGGBB)", Margin=new Thickness(0,0,0,5) }); stack.Children.Add(input);
   var preview=new Border { Height=42, CornerRadius=new CornerRadius(10), Background=new SolidColorBrush(background), Margin=new Thickness(0,0,0,12) }; stack.Children.Add(preview);
   input.TextChanged+=(s,e)=> { try { preview.Background=new SolidColorBrush(ParseColor(input.Text)); } catch(FormatException) {} };
   var choose=new Button { Content="Палитра Windows…", Padding=new Thickness(8,5,8,5), Margin=new Thickness(0,0,0,10) }; stack.Children.Add(choose);
   choose.Click+=(s,e)=> { using(var dialog=new Forms.ColorDialog { FullOpen=true }) { Color c=background; try { c=ParseColor(input.Text); } catch(FormatException) {} dialog.Color=System.Drawing.Color.FromArgb(c.R,c.G,c.B); if(dialog.ShowDialog(new DialogOwner(w))==Forms.DialogResult.OK)input.Text=ColorHex(Color.FromRgb(dialog.Color.R,dialog.Color.G,dialog.Color.B)); } };
   var apply=new Button { Content="Применить", IsDefault=true, Padding=new Thickness(8,6,8,6) }; stack.Children.Add(apply);
   apply.Click+=(s,e)=> { try { background=ParseColor(input.Text); UpdateBackground(); Save(); w.Close(); } catch(FormatException ex) { MessageBox.Show(w,ex.Message,"Цвет подложки"); } }; w.ShowDialog();
  }
  void Settings() {
   var w=new Window { Title="Настройки папки — GridNest", Width=460, Height=Math.Min(760,SystemParameters.WorkArea.Height-40), MinHeight=400, ResizeMode=ResizeMode.CanResize, WindowStartupLocation=WindowStartupLocation.CenterScreen, Owner=this, ShowInTaskbar=false };
   StackPanel sp=new StackPanel { Margin=new Thickness(28) }; w.Content=new ScrollViewer { Content=sp, VerticalScrollBarVisibility=ScrollBarVisibility.Auto, HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled };
   sp.Children.Add(new TextBlock { Text=name, FontSize=26, FontWeight=FontWeights.SemiBold, Margin=new Thickness(0,0,0,4) });
   var subtitle=new TextBlock { Text="Пространство для ваших файлов и приложений", FontSize=12, Margin=new Thickness(0,0,0,20), TextWrapping=TextWrapping.Wrap };subtitle.SetResourceReference(TextBlock.ForegroundProperty,"UiMuted");sp.Children.Add(subtitle);
   TextBox width=Field(sp,"СТОЛБЦЫ · 1–20",columns.ToString()); TextBox height=Field(sp,"РЯДЫ · 1–20",rows.ToString());
   var scaleLabel=new TextBlock { Margin=new Thickness(0,8,0,0) }; sp.Children.Add(scaleLabel);
   var size=new Slider { Minimum=50, Maximum=230, TickFrequency=5, IsSnapToTickEnabled=true, Value=iconSize/56.0*100, ToolTip="Масштаб значков" }; sp.Children.Add(size);
   Action scaleText=()=>scaleLabel.Text="МАСШТАБ · "+Math.Round(size.Value)+"% / "+Math.Min(128,Math.Round(size.Value*56/100))+" px"; size.ValueChanged+=(s,e)=>scaleText(); scaleText();
   var opacityLabel=new TextBlock(); sp.Children.Add(opacityLabel);
   var opacity=new Slider { Minimum=15, Maximum=100, Value=alpha*100, ToolTip="Непрозрачность" }; sp.Children.Add(opacity);
   Action opacityText=()=>opacityLabel.Text="НЕПРОЗРАЧНОСТЬ · "+Math.Round(opacity.Value)+"%"; opacity.ValueChanged+=(s,e)=>opacityText(); opacityText();
   var section=new TextBlock { Text="СОДЕРЖИМОЕ ПАПКИ", FontSize=11, Margin=new Thickness(0,10,0,0) };section.SetResourceReference(TextBlock.ForegroundProperty,"UiAccent");sp.Children.Add(section);
   var files=new RadioButton { Content="Файлы — перемещать оригиналы", IsChecked=storeFiles, GroupName="SettingsMode" }; sp.Children.Add(files);
   sp.Children.Add(new RadioButton { Content="Ярлыки — создавать ссылки", IsChecked=!storeFiles, GroupName="SettingsMode" });
   var motion=new CheckBox { Content="Анимации значков и панелей", IsChecked=animations }; sp.Children.Add(motion);
   var color=new Button { Content="Цвет подложки…", Margin=new Thickness(0,12,0,0) }; color.Click+=(s,e)=>ColorSettings(); sp.Children.Add(color);
   var button=new Button { Content="Сохранить настройки", Background=new SolidColorBrush(Color.FromRgb(50,112,112)), Foreground=Brushes.White, Margin=new Thickness(0,18,0,0) }; sp.Children.Add(button);
   button.Click+=(s,e)=> { int a,b; if(!int.TryParse(width.Text,out a)||a<1||a>20||!int.TryParse(height.Text,out b)||b<1||b>20) { MessageBox.Show(w,"Число столбцов и рядов должно быть от 1 до 20."); return; } storeFiles=files.IsChecked==true; animations=motion.IsChecked==true; animationMenu.IsChecked=animations; iconSize=Math.Max(28,Math.Min(128,(int)Math.Round(size.Value*56/100))); alpha=opacity.Value/100; UpdateBackground(); SetGrid(a,b); Clamp(); AlignPosition(); cache.Clear(); Refresh(); Save(); w.Close(); }; w.ShowDialog();
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
    SnapSize(257,319); UpdateLayout(); Save(); var x=XElement.Load(config); bool resize=Math.Abs((Width-50)/CellWidth-Math.Round((Width-50)/CellWidth))<.001&&Math.Abs((Height-78)/CellHeight-Math.Round((Height-78)/CellHeight))<.001&&Get(x,"Width",0)==Width&&Get(x,"Rows",0)==rows;
    var scrollBounds=scroll.TransformToAncestor(this).TransformBounds(new Rect(0,0,scroll.ActualWidth,scroll.ActualHeight)); var gripBounds=resizeHandle.TransformToAncestor(this).TransformBounds(new Rect(0,0,resizeHandle.ActualWidth,resizeHandle.ActualHeight)); bool corner=scrollBounds.Bottom<=gripBounds.Top+.01&&resizeHandle.ActualHeight<=12;
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
    return name+": file-to-lnk, duplicate names, copy existing lnk, preserve source="+(drop?"PASS":"FAIL "+string.Join(";",errors))+"; grid/persist="+(resize?"PASS":"FAIL")+"; shortcut icon="+(icon?"PASS":"FAIL")+"; rename/keep-extension="+(rename?"PASS":"FAIL")+"; folder rename="+(folderRename?"PASS":"FAIL")+"; rename conflict="+(conflict?"PASS":"FAIL")+"; inter-folder move="+(moved?"PASS":"FAIL")+"; manual order/persist="+(manual?"PASS":"FAIL")+"; color/persist/validation="+(color?"PASS":"FAIL")+"; resize-corner-clear="+(corner?"PASS":"FAIL")+"; animation-disable/persist="+(reduced?"PASS":"FAIL")+"; original-files-move/conflicts="+(originals&&recursive?"PASS":"FAIL")+"; icon-scale/persist="+(scaling?"PASS":"FAIL")+"; themed-scroll="+(themed?"PASS":"FAIL");
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


