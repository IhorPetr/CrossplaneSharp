namespace CrossplaneSharp
{

    /// <summary>
    /// Controls the output format of <see cref="NginxBuilder.Build"/>.
    /// </summary>
    public class BuildOptions
    {
        /// <summary>
        /// Spaces per indent level (ignored when <see cref="Tabs"/> is <c>true</c>).
        /// Default: 4.
        /// </summary>
        public int Indent { get; set; } = 4;

        /// <summary>
        /// Use a tab character instead of spaces for indentation.
        /// Default: <c>false</c>.
        /// </summary>
        public bool Tabs { get; set; } = false;

        /// <summary>
        /// Prepend a "built by crossplane" comment header.
        /// Default: <c>false</c>.
        /// </summary>
        public bool Header { get; set; } = false;
    }
}
