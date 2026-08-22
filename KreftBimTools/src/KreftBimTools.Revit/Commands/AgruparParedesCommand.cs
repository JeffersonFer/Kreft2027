using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using KreftBimTools.Core.Domain;
using KreftBimTools.Revit.Domain;
using KreftBimTools.Revit.Domain.Deteccao;
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

            foreach (var paredeRefe in paredesSelecionadas)
            {
                try
                {
                    var element = doc.GetElement(paredeRefe);
                    var entidade = factory.Criar(element);

                    if (entidade != null)
                        paredes.Add(entidade);
                }
                catch (Exception ex)
                {
                    erros.Add($"Elemento {paredeRefe.ElementId}: {ex.Message}");
                }
            }

            using (TransactionGroup tg = new TransactionGroup(doc, "Agrupador de paredes"))
            {
                tg.Start();

                foreach (var parede in paredes)
                {
                    try
                    {
                        AgruparParedes(doc, parede);
                    }
                    catch(Exception ex)
                    {
                        erros.Add($"Elemento {parede.Identificador}: {ex.Message}");
                    }
                }

                tg.Assimilate();
            }

            var resumo = $"{paredes.Count} parede(s) processada(s) com sucesso.";

            if (erros.Count > 0)
                resumo += $"\n\n{erros.Count} erro(s):\n{string.Join("\n", erros)}";

            TaskDialog.Show(nomeDoCommand, resumo);

            return Result.Succeeded;
        }

        private void AgruparParedes(Document doc, IElementoAlvenaria parede)
        {
            List<ElementId> elementIdsParaAgrupar = new List<ElementId>();

            var paredeId = new ElementId(long.Parse(parede.Identificador));
            Element paredeElement = doc.GetElement(paredeId);

            List<ElementId> dependentIds = new List<ElementId>();

            if (paredeElement is Wall wall)
            {
                // Cria filtro para categorias de portas e janelas
                ElementMulticategoryFilter filter = new ElementMulticategoryFilter(
                    new List<BuiltInCategory>
                    {
                             BuiltInCategory.OST_Doors,
                             BuiltInCategory.OST_Windows
                    }
                );

                // Obtém os IDs dos elementos dependentes
                dependentIds = wall.GetDependentElements(filter).ToList();
            }

            elementIdsParaAgrupar.Add(paredeId);
            elementIdsParaAgrupar.AddRange(dependentIds);

            var deteccaoBlocosNaParedeBB = new DeteccaoBoundingBox(doc, TipoElementoAlvenaria.Bloco).Detectar(parede);
            var deteccaoBlocosNaParedePontosDentroDoSolido = new DeteccaoSolidOrigin(doc).Refinar(parede, deteccaoBlocosNaParedeBB);
            var deteccaoBlocosNaParedePorOrientacao = new DeteccaoOrientacaoVetores(doc).Refinar(parede, deteccaoBlocosNaParedePontosDentroDoSolido);
            var blocosId = deteccaoBlocosNaParedePorOrientacao.Select(b => new ElementId(long.Parse(b.Identificador)));

            elementIdsParaAgrupar.AddRange(blocosId);

            var deteccaoGrauteHorizontalBB = new DeteccaoParedeGrauteHorizontal(doc).Detectar(parede);
            var deteccaoGrautesHorizontaisPontosDentroDoSolido = new DeteccaoSolidOrigin(doc).Refinar(parede, deteccaoGrauteHorizontalBB);
            var deteccaoGrautesHorizontaisNaParedePorOrientacao = new DeteccaoOrientacaoVetores(doc).Refinar(parede, deteccaoGrautesHorizontaisPontosDentroDoSolido);
            var grautesHorizontaisId = deteccaoGrautesHorizontaisNaParedePorOrientacao.Select(gh => new ElementId(long.Parse(gh.Identificador)));

            elementIdsParaAgrupar.AddRange(grautesHorizontaisId);

            if (elementIdsParaAgrupar.Count > 0)
            {
                using (Transaction tx = new Transaction(doc, "Agrupar paredes"))
                {
                    tx.Start();

                    doc.Create.NewGroup(elementIdsParaAgrupar);

                    tx.Commit();
                }
            }
        }
    }
}
