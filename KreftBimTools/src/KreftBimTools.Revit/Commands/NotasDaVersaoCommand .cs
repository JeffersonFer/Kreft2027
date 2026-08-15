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
            "• Estrutura inicial do plugin criada\n" +
            "• Suporte a Revit 2025, 2026 e 2027\n" +
            "• Instalador automatizado para as 3 versões"
        );

        return Result.Succeeded;
    }
}
