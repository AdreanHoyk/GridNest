using System;using System.IO;using System.IO.Compression;using System.Diagnostics;using System.Drawing;using System.Linq;using System.Reflection;using System.Threading;using System.Windows.Forms;
[assembly:AssemblyTitle("GridNest Setup")]
[assembly:AssemblyProduct("GridNest Setup")]
[assembly:AssemblyFileVersion("0.11.0.0")]
class Installer {
 static readonly string[] Files={"GridNest.exe","app.ico","Install.ps1","README.txt","LICENSE.txt"};
 static void Extract(string destination) {
  Directory.CreateDirectory(destination);
  using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("GridNest.Payload.zip"))using(var zip=new ZipArchive(stream,ZipArchiveMode.Read)) {
   if(zip.Entries.Count!=Files.Length||Files.Any(n=>!zip.Entries.Any(e=>e.FullName==n)))throw new IOException("Некорректный пакет установки.");
   foreach(var entry in zip.Entries)using(var source=entry.Open())using(var target=File.Create(Path.Combine(destination,entry.FullName)))source.CopyTo(target);
  }
 }
 static void Shortcut(string directory,string install) {
  Type type=Type.GetTypeFromProgID("WScript.Shell");object shell=Activator.CreateInstance(type);object link=null;
  try {link=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{Path.Combine(directory,"GridNest.lnk")});Type t=link.GetType();t.InvokeMember("TargetPath",BindingFlags.SetProperty,null,link,new object[]{Path.Combine(install,"GridNest.exe")});t.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,link,new object[]{install});t.InvokeMember("IconLocation",BindingFlags.SetProperty,null,link,new object[]{Path.Combine(install,"app.ico")+",0"});t.InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);}
  finally {if(link!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
 }
 static void StopApplication() {
  var processes=Process.GetProcessesByName("GridNest");if(processes.Length==0)return;
  try {using(var signal=EventWaitHandle.OpenExisting("Local\\GridNestStop"))signal.Set();}catch(WaitHandleCannotBeOpenedException) {throw new IOException("Закройте GridNest через меню в трее и повторите установку.");}
  foreach(var process in processes)if(!process.WaitForExit(6000))throw new IOException("GridNest ещё работает. Закройте его через меню в трее и повторите установку.");
 }
 [STAThread]static void Main(string[] args) {
  if(args.Length==2&&args[0]=="--verify") {try {string temp=Path.Combine(Path.GetTempPath(),"GridNest-Installer-Test-"+Guid.NewGuid().ToString("N"));Extract(temp);if(FileVersionInfo.GetVersionInfo(Path.Combine(temp,"GridNest.exe")).FileVersion!="0.11.0.0")throw new Exception("Version mismatch");File.WriteAllText(args[1],"PASS: installer payload extracted; application version 0.11.0; no user folders changed.");Environment.ExitCode=0;}catch(Exception e) {File.WriteAllText(args[1],"FAIL: "+e);Environment.ExitCode=1;}return;}
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"GridNest"),install=Path.Combine(data,".app");
  var form=new Form {Text="Установка GridNest",ClientSize=new Size(500,345),FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,StartPosition=FormStartPosition.CenterScreen,BackColor=Color.FromArgb(20,30,42),ForeColor=Color.FromArgb(237,243,248),Font=new Font("Segoe UI",10)};
  using(var iconStream=Assembly.GetExecutingAssembly().GetManifestResourceStream("GridNest.Setup.ico"))using(var icon=new Icon(iconStream))form.Icon=(Icon)icon.Clone();
  form.Controls.Add(new Label {Text="GridNest 0.11.0",Font=new Font("Segoe UI",23,FontStyle.Bold),AutoSize=true,Location=new Point(25,22)});
  form.Controls.Add(new Label {Text="Папки и файлы на рабочем столе",AutoSize=true,Location=new Point(27,77)});
  form.Controls.Add(new Label {Text="Установка для текущего пользователя.\nПапки, порядок и настройки сохраняются при обновлении.",AutoSize=true,Location=new Point(27,113)});
  var startup=new CheckBox {Text="Запускать GridNest при входе в Windows",Checked=true,AutoSize=true,Location=new Point(27,178)};form.Controls.Add(startup);
  var launch=new CheckBox {Text="Открыть GridNest после установки",Checked=true,AutoSize=true,Location=new Point(27,210)};form.Controls.Add(launch);
  var status=new Label {Text="",AutoSize=false,Size=new Size(445,38),Location=new Point(27,245),Font=new Font("Segoe UI",9)};form.Controls.Add(status);
  var button=new Button {Text="Установить / Обновить",Location=new Point(27,290),Size=new Size(445,36),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(50,112,112),ForeColor=Color.White};form.Controls.Add(button);
  button.Click+=(s,e)=> {
   button.Enabled=false;string staging=Path.Combine(Path.GetTempPath(),"GridNest-Install-"+Guid.NewGuid().ToString("N"));
   try {Extract(staging);StopApplication();Directory.CreateDirectory(install);string backup=Path.Combine(data,".backup-setup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(backup);
    foreach(string name in Files) {string target=Path.Combine(install,name);if(File.Exists(target))File.Copy(target,Path.Combine(backup,name));}
    try {foreach(string name in Files)File.Copy(Path.Combine(staging,name),Path.Combine(install,name),true);}catch {foreach(string name in Files)if(File.Exists(Path.Combine(backup,name)))File.Copy(Path.Combine(backup,name),Path.Combine(install,name),true);throw;}
    string marker=Path.Combine(install,"initialized");if(!File.Exists(marker)) {foreach(string name in new[]{"Работа","AI","Сеть","VR"})Directory.CreateDirectory(Path.Combine(data,name));File.WriteAllText(marker,"1");}
    Shortcut(Environment.GetFolderPath(Environment.SpecialFolder.Programs),install);string startupPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"GridNest.lnk");if(startup.Checked)Shortcut(Environment.GetFolderPath(Environment.SpecialFolder.Startup),install);else if(File.Exists(startupPath))File.Move(startupPath,Path.Combine(backup,"startup.lnk"));
    status.Text="Готово. Данные: "+data;if(launch.Checked)Process.Start(Path.Combine(install,"GridNest.exe"));button.Text="Установлено";MessageBox.Show(form,"GridNest установлен. Управление папками — через значок рядом с часами.","Готово",MessageBoxButtons.OK,MessageBoxIcon.Information);form.Close();
   }catch(Exception ex) {status.Text="Установка не завершена.";MessageBox.Show(form,ex.Message,"GridNest",MessageBoxButtons.OK,MessageBoxIcon.Information);button.Enabled=true;}
  };
  Application.Run(form);
 }
}
