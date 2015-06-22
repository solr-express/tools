using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConsoleApplication_Tool
{
    internal class Field
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public bool Stored { get; set; }
        public bool Indexed { get; set; }
        public bool OmitNorms { get; set; }
        public bool MultiValue { get; set; }

        public string OriginalName { get; set; }
    }

    class Program
    {
        private static string GetNetType(string solrType)
        {
            switch (solrType)
            {
                case "org.apache.solr.schema.TextField":
                case "org.apache.solr.schema.StrField":
                    return "string";
                case "org.apache.solr.schema.TrieDoubleField":
                    return "double";
                case "org.apache.solr.schema.TrieFloatField":
                    return "float";
                case "org.apache.solr.schema.TrieIntField":
                    return "int";
                case "org.apache.solr.schema.TrieLongField":
                    return "long";
                case "org.apache.solr.schema.TrieDateField":
                    return "DateTime";
                case "org.apache.solr.schema.BoolField":
                    return "boolean";
                case "org.apache.solr.schema.LatLonType":
                    return "GeoCoordinate";
                default:
                    throw new ArgumentOutOfRangeException("solrType");
            }
        }

        // Convert the string to Pascal case.
        private static string GetPascalCase(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return str;
            }

            if (str.Length < 2)
            {
                return str.ToUpper();
            }

            string[] words = str.Split(
                new char[] { '_' },
                StringSplitOptions.RemoveEmptyEntries);

            var result = string.Empty;
            foreach (string word in words)
            {
                result +=
                    word.Substring(0, 1).ToUpper() +
                    word.Substring(1);
            }

            return result;
        }

        static void Main(string[] args)
        {
            var collectionAddress = "http://localhost:8983/solr/collection1";

            var client = new RestClient(string.Concat(collectionAddress, "/admin/luke?wt=json&show=schema&indent=true"));
            var request = new RestRequest(Method.GET);
            var response = client.Execute(request);
            var content = response.Content;

            var jObj = JObject.Parse(content);

            var fields = new List<Field>();

            // Get fields
            foreach (var jField in jObj["schema"]["fields"])
            {
                var field = new Field();

                var flags = ((JProperty)(jField)).Value["flags"].Value<string>();

                field.OriginalName = ((JProperty)(jField)).Name;
                field.Name = ((JProperty)(jField)).Name;
                field.Type = ((JProperty)(jField)).Value["type"].Value<string>();
                field.Stored = flags.Contains("S");
                field.Indexed = flags.Contains("I");
                field.MultiValue = flags.Contains("M");
                field.OmitNorms = flags.Contains("O");

                fields.Add(field);
            }

            fields = fields.OrderBy(q => q.Name).ToList();

            var aliasTypes = new Dictionary<string, string>();

            // Get alias types
            foreach (var jField in jObj["schema"]["types"])
            {
                var alias = ((JProperty)(jField)).Name;
                var classType = ((JProperty)(jField)).Value["className"].Value<string>();

                aliasTypes.Add(alias, classType);
            }

            // Match alias and type
            foreach (var field in fields)
            {
                field.Type = GetNetType(aliasTypes[field.Type]);
                field.Name = GetPascalCase(field.Name);
            }

            // Create file content
            var className = GetPascalCase(collectionAddress.Split('/').Last());

            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using SolrExpress.Core.Attribute;");
            sb.AppendLine("using SolrExpress.Core.Query;");
            sb.AppendLine(string.Empty);
            sb.AppendFormat("namespace {0}.Context", "Actual.Namespace");
            sb.AppendLine();
            sb.AppendLine("{");
            sb.AppendFormat("\tpublic class {0}: IDocument", className);
            sb.AppendLine();
            sb.AppendLine("\t{");

            foreach (var field in fields)
            {
                var propType = field.MultiValue ? "List<{0}>" : "{0}";
                propType = string.Format(propType, field.Type);

                sb.AppendFormat(
                    "\t\t[SolrFieldAttribute(\"{0}\", Indexed = {1}, Stored = {2}, OmitNorms = {3})]",
                    field.OriginalName,
                    field.Indexed,
                    field.Stored,
                    field.OmitNorms);
                sb.AppendLine();
                sb.AppendFormat("\t\tpublic {0} {1} {{get; set;}}", propType, field.Name);
                sb.AppendLine();
                sb.AppendLine();
            }

            sb.AppendLine("\t}");
            sb.AppendLine("}");

            Console.WriteLine(sb.ToString());

            Console.Read();
        }
    }
}
