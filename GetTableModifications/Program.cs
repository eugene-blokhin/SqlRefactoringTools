using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GetTableModifications
{
    class Program
    {
        static int Main(string[] args)
        {

                        if (args.Length < 1)
                        {
                            Console.Error.WriteLine("File is not specified. Usage: GetTableModifications example.sql.");
                            return -1;
                        }
            
                        var filename = args[0];


            //var filename = @"C:\Users\Eugene Blokhin\Downloads\SPs\BinFetch.sql";

            if (!File.Exists(filename))
            {
                Console.Error.WriteLine("File was not found.");
                return -1;
            }

            using (var reader = new StreamReader(filename))
            {
                var parser = new TSql90Parser(false);

                IList<ParseError> errors;
                var result = parser.Parse(reader, out errors);




                if (errors.Count > 0)
                {
                    foreach (var error in errors)
                    {
                        Console.Error.WriteLine(error);
                    }

                    return -1;
                }

                var visitor = new TableUsageVisitor();
                result.Accept(visitor);


                List<object> outputList = new List<object>();

                visitor.InsertStatements
                    .Where(s => s.InsertSpecification.Target is NamedTableReference).ToList()
                    .ForEach(s =>
                        {
                            var target = ((NamedTableReference)s.InsertSpecification.Target);
                            var name = AliasVisitor.ResolveAlias(s, target.SchemaObject.BaseIdentifier.Value);

                            outputList.Add(new {Table = name, Action = "Insert"});
                        }
                    );

                visitor.UpdateStatements
                    .Where(s => s.UpdateSpecification.Target is NamedTableReference).ToList()
                    .ForEach(s =>
                        {
                            var columns = s.UpdateSpecification.SetClauses
                                .OfType<AssignmentSetClause>()
                                .Where(setClause => setClause.Column != null)
                                .Select(setClause => setClause.Column.MultiPartIdentifier.Identifiers[0].Value);
                            
                            var target = ((NamedTableReference)s.UpdateSpecification.Target);
                            var name = AliasVisitor.ResolveAlias(s, target.SchemaObject.BaseIdentifier.Value);

                            outputList.Add(new { Table = name, Action = "Update", Columns = columns});
                        }
                    );

                visitor.DeleteStatements
                    .Where(s => s.DeleteSpecification.Target is NamedTableReference).ToList()
                    .ForEach(s =>
                        {
                            var target = ((NamedTableReference)s.DeleteSpecification.Target);
                            var name = AliasVisitor.ResolveAlias(s, target.SchemaObject.BaseIdentifier.Value);

                            outputList.Add(new {Table = name, Action = "Delete"});
                        }
                    );

                Console.Write(JsonConvert.SerializeObject(outputList, Formatting.Indented));
            }

            return 0;
        }
    }


    public class AliasVisitor : TSqlFragmentVisitor
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

    public class TableUsageVisitor : TSqlFragmentVisitor
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
