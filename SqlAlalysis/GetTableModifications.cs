using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlRefactoringTools.SqlAlalysisModule
{
    [Cmdlet(VerbsCommon.Get, "TableModifications")]
    public class GetTableModifications : PSCmdlet
    {
        [Parameter(
            Mandatory = true,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            Position = 0,
            HelpMessage = "Script that contains definition of one or more stored procedures."
        )]
        [Alias("Definition")]
        public string Script { get; set; }
        
        protected override void ProcessRecord()
        {
            using (var reader = new StringReader(Script))
            {
                var parser = new TSql90Parser(false);

                IList<ParseError> errors;
                var result = parser.Parse(reader, out errors);

                if (errors.Count == 0)
                {
                    var visitor = new TableUsageVisitor();
                    result.Accept(visitor);
                    
                    visitor.InsertStatements
                        .Where(s => s.InsertSpecification.Target is NamedTableReference).ToList()
                        .ForEach(s =>
                            {
                                var target = ((NamedTableReference)s.InsertSpecification.Target);
                                var name = AliasVisitor.ResolveAlias(s, target.SchemaObject.BaseIdentifier.Value);

                                WriteObject(new { Table = name, Action = "Insert" });
                            }
                        );

                    visitor.UpdateStatements
                        .Where(s => s.UpdateSpecification.Target is NamedTableReference).ToList()
                        .ForEach(s =>
                            {
                                var columns = s.UpdateSpecification.SetClauses
                                .OfType<AssignmentSetClause>()
                                .Where(setClause => setClause.Column != null)
                                .Select(setClause => setClause.Column.MultiPartIdentifier.Identifiers.Last().Value);

                                var target = ((NamedTableReference)s.UpdateSpecification.Target);
                                var name = AliasVisitor.ResolveAlias(s, target.SchemaObject.BaseIdentifier.Value);

                                WriteObject(new { Table = name, Action = "Update", Columns = columns });
                            }
                        );

                    visitor.DeleteStatements
                        .Where(s => s.DeleteSpecification.Target is NamedTableReference).ToList()
                        .ForEach(s =>
                            {
                                var target = ((NamedTableReference)s.DeleteSpecification.Target);
                                var name = AliasVisitor.ResolveAlias(s, target.SchemaObject.BaseIdentifier.Value);

                                WriteObject(new { Table = name, Action = "Delete" });
                            }
                        );
                }
                else
                {
                    foreach (var error in errors)
                    {
                        var errorRecord = new ErrorRecord(
                            new FormatException($"{error.Message} Line:{error.Line}:{error.Column}"),
                            null,
                            ErrorCategory.InvalidArgument,
                            Script
                        );

                        WriteError(errorRecord);
                    }
                }
            }
        }

        private class AliasVisitor : TSqlFragmentVisitor
        {
            public Dictionary<string, string> Aliases { get; } = new Dictionary<string, string>();

            public override void ExplicitVisit(NamedTableReference node)
            {
                if (node.Alias != null)
                {
                    Aliases[node.Alias.Value] = node.SchemaObject.BaseIdentifier.Value;
                }

                base.ExplicitVisit(node);
            }

            public static string ResolveAlias(TSqlFragment fragment, string alias)
            {
                var visitor = new AliasVisitor();
                fragment.Accept(visitor);

                return visitor.Aliases.ContainsKey(alias)
                    ? visitor.Aliases[alias]
                    : alias;
            }
        }

        private class TableUsageVisitor : TSqlFragmentVisitor
        {
            public List<SelectStatement> SelectStatements { get; } = new List<SelectStatement>();
            public List<InsertStatement> InsertStatements { get; } = new List<InsertStatement>();
            public List<DeleteStatement> DeleteStatements { get; } = new List<DeleteStatement>();
            public List<UpdateStatement> UpdateStatements { get; } = new List<UpdateStatement>();

            public override void ExplicitVisit(DeleteStatement node)
            {
                DeleteStatements.Add(node);
                base.ExplicitVisit(node);
            }

            public override void ExplicitVisit(SelectStatement node)
            {
                SelectStatements.Add(node);
                base.ExplicitVisit(node);
            }

            public override void ExplicitVisit(UpdateStatement node)
            {
                UpdateStatements.Add(node);
                base.ExplicitVisit(node);
            }

            public override void ExplicitVisit(InsertStatement node)
            {
                InsertStatements.Add(node);
                base.ExplicitVisit(node);
            }
        }
    }
}
