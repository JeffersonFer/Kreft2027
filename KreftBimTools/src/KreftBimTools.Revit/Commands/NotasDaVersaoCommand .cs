using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using System.Reflection;

namespace KreftBimTools.Revit.Commands;

[Transaction(TransactionMode.Manual)]
public class NotasDaVersaoCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, Autodesk.Revit.DB.ElementSet elements)
    {
        var versao = Assembly.GetExecutingAssembly().GetName().Version;
        var versaoFormatada = $"{versao.Major}.{versao.Minor}.{versao.Build}";

        TaskDialog.Show(
            "KreftBimTools - Notas da Versão",
            $"Versão {versaoFormatada}\n\n" +
            "• Estrutura inicial do plugin (Core, Revit, UI)\n" +
            "• Suporte a Revit 2025, 2026 e 2027\n" +
            "• Instalador automatizado com detecção de versão\n" +
            "• Ribbon: abas K-Alvenaria e K-Concreto\n" +
            "• Comando: Notas da Versão\n" +
            "• Comando: Agrupar Paredes - concluído\n" +
            "• Comando: Armador de Paredes - cintas, vergas e contravergas em aço horizontal\n" +
            "• Comando: Compatibilizador de Blocos - ajusta blocos atravessados por portas/janelas\n" +
            "• Comando: Coletor de Corpus - coleta dados de modulação de alvenaria para futura IA\n" +
            "• Correção: Armador de Paredes agora funciona corretamente em paredes com blocos agrupados\n" +
            "• Correção: REBAIXOA/REBAIXOB do aço horizontal agora são calculados para cintas, vergas e contravergas"
        );

        return Result.Succeeded;
    }
}
