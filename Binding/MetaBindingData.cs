namespace Godot.Community.ControlBinding
{
    public readonly struct MetaBindingData
    {
        public readonly string PropertyPath;
        public readonly BindingMode Mode;

        public MetaBindingData(string propertyPath, BindingMode mode)
        {
            PropertyPath = propertyPath;
            Mode = mode;
        }
    }
}