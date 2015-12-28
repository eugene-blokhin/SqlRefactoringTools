using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Runtime.CompilerServices;
using Microsoft.PowerShell.Commands;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlRefactoringTools.SqlAlalysisModule
{
    [Cmdlet(VerbsCommon.Get, "StoredProcedureArguments")]
    public class GetStoredProcedureArgumentsCmdLet : PSCmdlet
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
                    var visitor = new ArgumentsVisitor();
                    result.Accept(visitor);
                    
                    WriteObject(new
                    {
                        ProcedureName = visitor.ProcedureName,
                        Parameters = visitor.Parameters
                    });
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

        private class ArgumentsVisitor : TSqlFragmentVisitor
        {
            private readonly List<ProcedureParameterInfo> _parameters = new List<ProcedureParameterInfo>();

            public ProcedureNameInfo? ProcedureName { get; private set; }
            public IEnumerable<ProcedureParameterInfo> Parameters => _parameters;

            public override void ExplicitVisit(ProcedureParameter node)
            {
                _parameters.Add(new ProcedureParameterInfo(
                    node.VariableName.Value, 
                    node.DataType.Name.BaseIdentifier.Value)
                );

                base.ExplicitVisit(node);
            }

            public override void ExplicitVisit(CreateProcedureStatement node)
            {
                if (ProcedureName != null)
                {
                    throw new FormatException("Only one procedure definition is allowed per a script");
                }

                ProcedureName = new ProcedureNameInfo(
                    node.ProcedureReference.Name.BaseIdentifier.Value, 
                    node.ProcedureReference.Name.SchemaIdentifier?.Value
                );
                base.ExplicitVisit(node);
            }
        }

        public struct ProcedureParameterInfo
        {
            public ProcedureParameterInfo(string name, string type)
            {
                Name = name;
                Type = type;
            }

            public string Name { get; private set; }
            public string Type { get; private set; }

            public override string ToString()
            {
                return $"{Name} : {Type}";
            }
        }

        public struct ProcedureNameInfo
        {
            public ProcedureNameInfo(string baseName, string schemaName)
            {
                BaseName = baseName;
                SchemaName = schemaName;
            }

            public string BaseName { get; private set; }
            public string SchemaName { get; private set; }

            public override string ToString()
            {
                return SchemaName != null
                    ? $"[{SchemaName}].[{BaseName}]"
                    : $"[{BaseName}]";
            }
        }
    }
}
