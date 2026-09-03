using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace KreftBimTools.Revit.Application;

public class App : IExternalApplication
{
    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            CreateRibbonTab(application, "K-Alvenaria");
            CreateRibbonTab(application, "K-Concreto");

            CreateButton(application, "K-Alvenaria", "Geral", "NotasDaVersaoButton",
                "Notas da\nVersão", "KreftBimTools.Revit.Commands.NotasDaVersaoCommand",
                "Exibe as notas da versão atual do KreftBimTools",
                "NotasDaVersao32.png", "NotasDaVersao16.png");

            CreateButton(application, "K-Concreto", "Geral", "NotasDaVersaoButton",
                "Notas da\nVersão", "KreftBimTools.Revit.Commands.NotasDaVersaoCommand",
                "Exibe as notas da versão atual do KreftBimTools",
                "NotasDaVersao32.png", "NotasDaVersao16.png");

            CreateButton(application, "K-Alvenaria", "Paredes", "AgruparParedesButton",
                "Agrupar\nElementos", "KreftBimTools.Revit.Commands.AgruparParedesCommand",
                "Agrupa elementos de alvenaria estrutural relacionados a uma parede",
                "AgruparParedes32.png", "AgruparParedes16.png");

            CreateButton(application, "K-Alvenaria", "Blocos", "CompatibilizadorBlocosButton",
                "Compatibilizar", "KreftBimTools.Revit.Commands.CompatibilizadorBlocos",
                "Verifica aberturas nas paredes e compatibiliza os blocos que ficam parcialmente atravessados",
                "CompatibilizarBlocos32.png", "CompatibilizarBlocos16.png");

            CreateButton(application, "K-Alvenaria", "Aço", "ArmadorDeParedesButton",
                "Aço\nHorizontal", "KreftBimTools.Revit.Commands.ArmadorDeParedes",
                "Calcula e cria automaticamente o aço horizontal de cintas, vergas e contravergas",
                "ArmadorDeParedes32.png", "ArmadorDeParedes16.png");

            CreateButton(application, "K-Alvenaria", "Machine Learning", "ColetarCorpusButton",
                "Coletar\nCorpus", "KreftBimTools.Revit.Commands.ColetarCorpusCommand",
                "Coleta dados de modulação de blocos em trechos de parede, para análise e aprendizagem de máquina",
                "ColetarCorpus32.png", "ColetarCorpus16.png");
        }
        catch (System.Exception ex)
        {
            TaskDialog.Show("KreftBimTools - Erro no OnStartup", ex.ToString());
        }

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

    private static void CreateButton(
        UIControlledApplication application,
        string tabName,
        string panelName,
        string buttonName,
        string buttonText,
        string commandClassName,
        string toolTip,
        string? largeImageFile = null,
        string? smallImageFile = null)
    {
        var panel = GetOrCreatePanel(application, tabName, panelName);

        var buttonData = new PushButtonData(
            buttonName,
            buttonText,
            Assembly.GetExecutingAssembly().Location,
            commandClassName
        );

        buttonData.ToolTip = toolTip;

        if (largeImageFile is not null)
            buttonData.LargeImage = LoadImage(largeImageFile);

        if (smallImageFile is not null)
            buttonData.Image = LoadImage(smallImageFile);

        panel.AddItem(buttonData);
    }

    private static RibbonPanel GetOrCreatePanel(UIControlledApplication application, string tabName, string panelName)
    {
        var panels = application.GetRibbonPanels(tabName);
        var panelExistente = panels.FirstOrDefault(p => p.Name == panelName);

        return panelExistente ?? application.CreateRibbonPanel(tabName, panelName);
    }

    private static BitmapImage LoadImage(string fileName)
    {
        var assemblyFolder = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var imagePath = System.IO.Path.Combine(assemblyFolder!, "Resources", fileName);

        return new BitmapImage(new System.Uri(imagePath, System.UriKind.Absolute));
    }
}