using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using KreftBimTools.Core.Domain;
using KreftBimTools.Revit.Domain;
using KreftBimTools.Revit.Domain.SelectionFilters;

namespace KreftBimTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    internal class AgruparParedesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;

            string nomeDoCommand = "Agrupador de paredes";

            var erros = new List<string>();

            //Selecionar uma ou mais paredes
            TaskDialog.Show(nomeDoCommand, "Selecione as paredes que deseja agrupar.");

            List<Reference> paredesSelecionadas;

            try
            {
                paredesSelecionadas = uidoc.Selection
                    .PickObjects(
                    ObjectType.Element,
                    new ParedeEstruturalFilter(),
                    "Selecione uma ou mais paredes estruturais para o agrupamento."
                    ).ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                TaskDialog.Show(nomeDoCommand, "Comando cancelado.");
                return Result.Cancelled;
            }

            var factory = new RevitElementoFactory();
            var paredes = new List<IElementoAlvenaria>();

            foreach (var referencia in paredesSelecionadas)
            {
                try
                {
                    var element = doc.GetElement(referencia);
                    var entidade = factory.Criar(element);

                    if (entidade != null)
                        paredes.Add(entidade);
                }
                catch (Exception ex)
                {
                    erros.Add($"Elemento {referencia.ElementId}: {ex.Message}");
                }
            }

            var resumo = $"{paredes.Count} parede(s) processada(s) com sucesso.";

            if (erros.Count > 0)
                resumo += $"\n\n{erros.Count} erro(s):\n{string.Join("\n", erros)}";

            TaskDialog.Show(nomeDoCommand, resumo);

            return Result.Succeeded;
        }
    }
}
