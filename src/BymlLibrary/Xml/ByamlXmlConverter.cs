using BymlLibrary.Nodes.Containers;
using Revrs;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace BymlLibrary.Xml
{
    public class ByamlXmlConverter
    {
        private static readonly XNamespace _yamlconvNs = "yamlconv";

        public static Byml FromXML(string text)
        {
            return FromXDocument(XDocument.Parse(text));
        }

        internal static Byml FromXDocument(XDocument xDocument)
        {
            if (xDocument == null)
                throw new ArgumentNullException(nameof(xDocument));
            if (xDocument.Root?.Name != "yaml")
                throw new ArgumentException("Incompatible XML data. A \"yaml\" root element is required.");

            return ReadNode(xDocument.Root);
        }

        private static Byml ReadNode(XElement node)
        {
            Byml convertValue(string value)
            {
                if (value == "null")
                    return new Byml();
                if (value == string.Empty)
                    return new Byml("");
                else if (value == "true")
                    return new Byml(true);
                else if (value == "false")
                    return new Byml(false);
                else if (value.EndsWith("f"))
                    return new Byml(float.Parse(value.Substring(0, value.Length - "f".Length), CultureInfo.InvariantCulture));
                else if (value.EndsWith("u"))
                    return new Byml(uint.Parse(value.Substring(0, value.Length - "u".Length), CultureInfo.InvariantCulture));
                else if (value.EndsWith("i64"))
                    return new Byml(long.Parse(value.Substring(0, value.Length - "i64".Length), CultureInfo.InvariantCulture));
                else if (value.EndsWith("u64"))
                    return new Byml(ulong.Parse(value.Substring(0, value.Length - "u64".Length), CultureInfo.InvariantCulture));
                else if (value.EndsWith("d"))
                    return new Byml(double.Parse(value.Substring(0, value.Length - "d".Length), CultureInfo.InvariantCulture));
                else
                    return new Byml(int.Parse(value, CultureInfo.InvariantCulture));
            }

            string convertString() => node.Value.ToString();

            BymlPath convertPath()
            {
                List<BymlPathPoint> points = new();
                foreach (XElement pathPoint in node.Elements("point"))
                    points.Add(convertPathPoint(pathPoint));
                return new BymlPath() { Points = points.ToArray(), };
            }

            BymlPathPoint convertPathPoint(XElement pathPoint)
            {
                return new BymlPathPoint
                {
                    Position = new System.Numerics.Vector3(
                        convertValue(pathPoint.Attribute("x")?.Value ?? "0f").GetFloat(),
                        convertValue(pathPoint.Attribute("y")?.Value ?? "0f").GetFloat(),
                        convertValue(pathPoint.Attribute("z")?.Value ?? "0f").GetFloat()),
                    Normal = new System.Numerics.Vector3(
                        convertValue(pathPoint.Attribute("nx")?.Value ?? "0f").GetFloat(),
                        convertValue(pathPoint.Attribute("ny")?.Value ?? "0f").GetFloat(),
                        convertValue(pathPoint.Attribute("nz")?.Value ?? "0f").GetFloat()),
                    Value = convertValue(pathPoint.Attribute("val")?.Value ?? "0").GetUInt32()
                };
            }

            BymlMap convertDictionary()
            {
                BymlMap dictionary = new();
                foreach (XElement element in node.Elements())
                    dictionary.Add(XmlConvert.DecodeName(element.Name.ToString()), ReadNode(element));
                // Only keep non-namespaced attributes for now to filter out yamlconv and xml(ns) ones.
                foreach (XAttribute attribute in node.Attributes().Where(x => x.Name.Namespace == XNamespace.None))
                    dictionary.Add(XmlConvert.DecodeName(attribute.Name.ToString()), convertValue(attribute.Value));
                return dictionary;
            }

            BymlArray convertArray()
            {
                BymlArray array = new();
                foreach (XElement element in node.Elements("value"))
                    array.Add(ReadNode(element));
                return array;
            }


            // Detecting the special "type" attribute like this is unsafe as it could also be a dictionary with a "type"
            // key. Yamlconv should have namespaced its attribute to safely identify it.
            switch (node.Attributes("type").SingleOrDefault()?.Value)
            {
                // TODO: Add null support. Can null be set for value types?
                // TODO: Add reference support. Use Element with encoded XPath.
                case null when node.HasAttributes || node.HasElements: return convertDictionary();
                case null: return convertValue(node.Value);
                case "array": return convertArray();
                case "path": return convertPath();
                case "string": return convertString();
                default: throw new Exception("Unknown XML contents.");
            }
        }

        public static string ToXML(Byml root, Endianness endianness, int version)
        {
            XDocument doc = ToXDocument(root, endianness, version);
            using (StringWriter writer = new StringWriter())
            {
                doc.Save(writer);
                return writer.ToString();
            }
        }

        internal static XDocument ToXDocument(Byml root, Endianness endianness, int version)
        {
            var rootNode = (XElement)SaveNode("yaml", root, false);

            List<XAttribute> attribs = rootNode.Attributes().ToList();
            attribs.Insert(0, new XAttribute(XNamespace.Xmlns + "yamlconv", "yamlconv"));
            attribs.Insert(1, new XAttribute(_yamlconvNs + "endianness",
                endianness == Endianness.Little ? "little" : "big"));
            attribs.Insert(2, new XAttribute(_yamlconvNs + "offsetCount", false ? 4 : 3));
            attribs.Insert(3, new XAttribute(_yamlconvNs + "byamlVersion", version));
            rootNode.Attributes().Remove();
            rootNode.Add(attribs);

            return new XDocument(new XDeclaration("1.0", "utf-8", null), rootNode);
        }

        internal static XObject SaveNode(string name, Byml node, bool isArrayElement)
        {
            XObject convertValue(object value)
             => isArrayElement ? new XElement(name, value) : new XAttribute(name, value);

            XElement convertString(string stringNode)
                => new XElement(name, new XAttribute("type", "string"), stringNode);

            XElement convertPathPoint(BymlPathPoint pathPointNode)
            {
                return new XElement("point",
                    new XAttribute("x", getSingleString(pathPointNode.Position.X)),
                    new XAttribute("y", getSingleString(pathPointNode.Position.Y)),
                    new XAttribute("z", getSingleString(pathPointNode.Position.Z)),
                    new XAttribute("nx", getSingleString(pathPointNode.Normal.X)),
                    new XAttribute("ny", getSingleString(pathPointNode.Normal.Y)),
                    new XAttribute("nz", getSingleString(pathPointNode.Normal.Z)),
                    new XAttribute("val", getUInt32String(pathPointNode.Value)));
            }

            XObject convertPath(BymlPath pathNode)
            {
                XElement xElement = new XElement(name, new XAttribute("type", "path"));
                foreach (var element in pathNode.Points)
                    xElement.Add(convertPathPoint(element));
                return xElement;
            }


            XElement convertDictionary(BymlMap dictionaryNode)
            {
                XElement xElement = new XElement(name);
                foreach (var element in dictionaryNode.OrderBy(x => x.Key, StringComparer.Ordinal))
                    xElement.Add(SaveNode(element.Key, element.Value, false));
                return xElement;
            }

            XElement convertArray(BymlArray arrayNode)
            {
                XElement xElement = new XElement(name, new XAttribute("type", "array"));
                foreach (dynamic element in arrayNode)
                    xElement.Add(SaveNode("value", element, true));
                return xElement;
            }

            string getBooleanString(bool value) => value ? "true" : "false";
            string getInt32String(int value) => value.ToString(CultureInfo.InvariantCulture);
            string getSingleString(float value) => value.ToString(CultureInfo.InvariantCulture) + "f";
            string getUInt32String(uint value) => value.ToString(CultureInfo.InvariantCulture) + "u";
            string getInt64String(long value) => value.ToString(CultureInfo.InvariantCulture) + "i64";
            string getUInt64String(ulong value) => value.ToString(CultureInfo.InvariantCulture) + "u64";
            string getDoubleString(double value) => value.ToString(CultureInfo.InvariantCulture) + "d";

            if (node.Value == null) return convertString("null");

            name = XmlConvert.EncodeName(name);
            switch (node.Type)
            {
                case BymlNodeType.Bool: return convertValue(getBooleanString(node.GetBool()));
                case BymlNodeType.Int: return convertValue(getInt32String(node.GetInt()));
                case BymlNodeType.UInt32: return convertValue(getUInt32String(node.GetUInt32()));
                case BymlNodeType.Float: return convertValue(getSingleString(node.GetFloat()));
                case BymlNodeType.UInt64: return convertValue(getUInt64String(node.GetUInt64()));
                case BymlNodeType.Int64: return convertValue(getInt64String(node.GetInt64()));
                case BymlNodeType.String: return convertString(node.GetString());
                case BymlNodeType.MK8PathIndex: return convertPath(node.GetPath());
                case BymlNodeType.Array: return convertArray(node.GetArray());
                case BymlNodeType.Map: return convertDictionary(node.GetMap());
                default:
                    throw new Exception("Unsupported node type! " + node.GetType());
            }
        }
    }
}
