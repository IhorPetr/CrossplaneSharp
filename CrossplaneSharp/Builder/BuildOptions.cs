namespace CrossplaneSharp
{

    /// <summary>
    /// Controls the output format of <see cref="NginxBuilder.Build"/>.
    /// Mirrors the parameters of the Python crossplane <c>build()</c> function.
    /// </summary>
    public class BuildOptions
    {
        /// <summary>
        /// Spaces per indent level (ignored when <see cref="Tabs"/> is <c>true</c>).
        /// Equivalent to Python <c>indent</c>. Default: 4.
        /// </summary>
        public int Indent { get; set; } = 4;

        /// <summary>
        /// Use a tab character instead of spaces for indentation.
        /// Equivalent to Python <c>tabs</c>. Default: <c>false</c>.
        /// </summary>
        public bool Tabs { get; set; } = false;

        /// <summary>
        /// Prepend a "built by crossplane" comment header.
        /// Equivalent to Python <c>header</c>. Default: <c>false</c>.
        /// </summary>
        public bool Header { get; set; } = false;
    }
}
