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
            "• Comando: Agrupar Paredes (seleção múltipla de paredes estruturais)\n" +
            "• Detecção automática de Portas e Janelas relacionadas à parede\n" +
            "• Pipeline de detecção por proximidade: BoundingBox + Solid/Origin + Orientação (Blocos)\n" +
            "• Versão em desenvolvimento ativo - detecção de Graute Horizontal ainda em ajuste"
        );

        return Result.Succeeded;
    }
}
