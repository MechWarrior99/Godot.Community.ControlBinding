using System;
using Godot;
using Godot.Collections;

namespace Godot.Community.ControlBinding
{
    public partial class BindingController : Control
    {
        private const string bindingDataPathKey = "Path=";
        private const string bindingDataModeKey = "Mode=";
        private const string dataSourceName = "BindingDataSource";
        private const string bindingKeyPrefix = "Binding_";
        
        private static System.Collections.Generic.Dictionary<Guid, BindingContext> _dataSources = new System.Collections.Generic.Dictionary<Guid, BindingContext>();

        public override void _Ready()
        {
            // Callback so we can auto bind any node as it is added, via its metadata (if it has any)
            // We can't use 'ChildEnteredTree' because that is only direct children.
            GetTree().NodeAdded += OnNodeAdded;
        }
        
        private void OnNodeAdded(Node node)
        {
            // We only support binding control nodes.
            if (node is not Control)
                return;
            
            // Skip hidden/internal nodes.
            if (node.Name.ToString().StartsWith("@@"))
                return;

            // We check if the added node is a descendant of this node because the callback is for the entire node tree. 
            if (!((string)node.GetPath()).Contains(GetPath()))
                return;
            
            // Get the first BindingContext from the node's ancestors.
            BindingContext ctx = FindAncestorBindingContext(node);
            if (ctx != null)
                BindFromMetaData(node, ctx);
        }
        
        public void BindToContext(Control control, BindingContext context)
        {
            // We map the context to a GUID which can be stored in the metadata of the control.
            // We later use this when a child node is added to find what context it should auto bind to (if any).
            var guid = Guid.NewGuid();
            _dataSources.Add(guid, context);
            control.SetMeta(dataSourceName, guid.ToByteArray()); 
            
            BindDescendants(control, context);
        }
        

        private void BindDescendants(Node node, BindingContext ctx)
        {
            BindFromMetaData(node, ctx);
            
            foreach (Node child in node.GetChildren())
            {
                if (child.HasMeta(dataSourceName))
                    continue;

                BindDescendants(child, ctx);
            }
        }

        private void BindFromMetaData(Node control, BindingContext ctx)
        {
            if (TryGetBoundContext(control, out BindingContext boundContext))
            {
                if (boundContext != ctx)
                    return;
            }

            foreach (string metaDataName in control.GetMetaList())
            {
                // We only want MetaData with our 'binding key' prefix in the name.
                if (!metaDataName.StartsWith(bindingKeyPrefix)) 
                    continue;

                // The metadata value is a string following the pattern of other .NET UI frameworks
                // I.e. "Path=MyViewModelValue, Mode=TwoWay".
                string metaData = control.GetMeta(metaDataName).As<string>();
                MetaBindingData bindingData = ParseMetaBindingData(metaData);
                    
                // Metadata binding follows the pattern of having the name start with the binding key "Binding_xxx".
                // i.e. "Binding_Value", "Binding_Text".
                // We remove the binding key "Binding_" from the meta name to get the path of the property.
                string controlPropertyName = metaDataName.Substring(bindingKeyPrefix.Length);
                
                // Bind the control property to data property.
                ctx.BindProperty(control, controlPropertyName, bindingData.PropertyPath, bindingData.Mode);
            }
        }

        private BindingContext FindAncestorBindingContext(Node node)
        {
            if (TryGetBoundContext(node, out BindingContext boundContext))
                return boundContext;

            for (Node current = node; current != null; current = current.GetParent())
            {
                if (TryGetBoundContext(current, out boundContext))
                    return boundContext;
            }

            return null;
        }
        
        private bool TryGetBoundContext(Node node, out BindingContext ctx)
        {
            ctx = null;
            
            if (!node.HasMeta(dataSourceName))
                return false;

            var contextIdBytes = node.GetMeta(dataSourceName).AsByteArray();
            var guid = new Guid(contextIdBytes);

            return _dataSources.TryGetValue(guid, out ctx);
        }
        
        /// <summary>
        /// Get the actual typed binding data stored in a metaData string value.
        /// </summary>
        /// <param name="metaData">The MetaData value to try getting data from.</param>
        /// <returns></returns>
        private MetaBindingData ParseMetaBindingData(string metaData)
        {
            // Entries in the metaData is comma separated, and not order dependent
            // The format is: "Path=MyViewModelValue, Mode=TwoWay".
            string[] entries = metaData.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            string propertyName = string.Empty;
            BindingMode mode = BindingMode.OneWay;
            foreach (string entry in entries)
            {
                if (entry.StartsWith(bindingDataPathKey))
                {
                    propertyName = entry.Remove(0, bindingDataPathKey.Length);
                    continue;
                }
                
                if (entry.StartsWith(bindingDataModeKey))
                {
                    string bindingModeName = entry.Substring(bindingDataModeKey.Length);
                    
                    if (!Enum.TryParse(bindingModeName, true, out mode))
                    {
                        GD.PrintErr($"Invalid binding mode '{entry.Remove(0, bindingDataModeKey.Length)}'.");
                    }

                    continue;
                }
            }

            return new MetaBindingData(propertyName, mode);
        }
    }
}