using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;

namespace KreftBimTools.Revit.Commands;

[Transaction(TransactionMode.Manual)]
public class NotasDaVersaoCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, Autodesk.Revit.DB.ElementSet elements)
    {
        TaskDialog.Show(
            "KreftBimTools - Notas da Versão",
            "Versão 1.0.0\n\n" +
            "• Estrutura inicial do plugin (Core, Revit, UI)\n" +
            "• Suporte a Revit 2025, 2026 e 2027\n" +
            "• Instalador automatizado com detecção de versão\n" +
            "• Ribbon: abas K-Alvenaria e K-Concreto\n" +
            "• Comando: Notas da Versão\n" +
            "• Comando: Agrupar Paredes (seleção múltipla de paredes estruturais)\n" +
            "• Arquitetura de detecção por proximidade (BoundingBox) para Blocos"
        );

        return Result.Succeeded;
    }
}
