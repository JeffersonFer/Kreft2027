using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace KreftBimTools.Revit.Application;

public class App : IExternalApplication
{
    public Result OnStartup(UIControlledApplication application)
    {
        CreateRibbonTab(application, "K-Alvenaria");
        CreateRibbonTab(application, "K-Concreto");

        CreateNotasDaVersaoButton(application, "K-Alvenaria");
        CreateNotasDaVersaoButton(application, "K-Concreto");

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        return Result.Succeeded;
    }

    private static void CreateRibbonTab(UIControlledApplication application, string tabName)
    {
        try
        {
            application.CreateRibbonTab(tabName);
        }
        catch (Autodesk.Revit.Exceptions.ArgumentException)
        {
            // Aba já existe - ignora
        }
    }

    private static void CreateNotasDaVersaoButton(UIControlledApplication application, string tabName)
    {
        var panel = application.CreateRibbonPanel(tabName, "Geral");

        var buttonData = new PushButtonData(
            "NotasDaVersaoButton",
            "Notas da\nVersão",
            Assembly.GetExecutingAssembly().Location,
            "KreftBimTools.Revit.Commands.NotasDaVersaoCommand"
        );

        buttonData.LargeImage = LoadImage("NotasDaVersao32.png");
        buttonData.Image = LoadImage("NotasDaVersao16.png");
        buttonData.ToolTip = "Exibe as notas da versão atual do KreftBimTools";

        panel.AddItem(buttonData);
    }

    private static BitmapImage LoadImage(string fileName)
    {
        var assemblyFolder = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var imagePath = System.IO.Path.Combine(assemblyFolder!, "Resources", fileName);

        return new BitmapImage(new System.Uri(imagePath, System.UriKind.Absolute));
    }
}