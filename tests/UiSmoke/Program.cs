using System.Reflection;

var assembly = Assembly.Load("VFL.EditorAutomatico");
var formType = assembly.GetType("VFL.EditorAutomatico.MainForm", true)!;
using var form = (Form)Activator.CreateInstance(formType, nonPublic: true)!;
form.Show();
Application.DoEvents();
using var image = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
form.DrawToBitmap(image, new Rectangle(Point.Empty, form.ClientSize));
image.Save(args[0]);
form.Close();
