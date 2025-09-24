namespace CrossplaneSharp;

internal static class DirectiveArgument
{
    public const uint NGX_CONF_NOARGS = 0x00000001;
    public const uint NGX_CONF_TAKE1  = 0x00000002;
    public const uint NGX_CONF_TAKE2 = 0x00000004;
    public const uint NGX_CONF_TAKE3 = 0x00000008;
    public const uint NGX_CONF_TAKE4 = 0x00000010;
    public const uint NGX_CONF_TAKE5 = 0x00000020;
    public const uint NGX_CONF_TAKE6 = 0x00000040;
    public const uint NGX_CONF_TAKE7 = 0x00000080;
    public const uint NGX_CONF_BLOCK = 0x00000100;
    public const uint NGX_CONF_FLAG = 0x00000200;
    public const uint NGX_CONF_ANY = 0x00000400;
    public const uint NGX_CONF_1MORE = 0x00000800;
    public const uint NGX_CONF_2MORE = 0x00001000;
    public const uint NGX_CONF_TAKE12  = NGX_CONF_TAKE1 | NGX_CONF_TAKE2;
    public const uint NGX_CONF_TAKE13  = NGX_CONF_TAKE1 | NGX_CONF_TAKE3;
    public const uint NGX_CONF_TAKE23  = NGX_CONF_TAKE2 | NGX_CONF_TAKE3;
    public const uint NGX_CONF_TAKE34  = NGX_CONF_TAKE3 | NGX_CONF_TAKE4;
    public const uint NGX_CONF_TAKE123  = NGX_CONF_TAKE12 | NGX_CONF_TAKE3;
    public const uint NGX_CONF_TAKE1234  = NGX_CONF_TAKE123 | NGX_CONF_TAKE4;
}