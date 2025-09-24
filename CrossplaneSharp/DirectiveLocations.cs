namespace CrossplaneSharp;

internal static class DirectiveLocations
{
    public const uint NGX_DIRECT_CONF       = 0x00010000; // main file (not used)
    public const uint NGX_MAIN_CONF         = 0x00040000; // main context
    public const uint NGX_EVENT_CONF        = 0x00080000; // events
    public const uint NGX_MAIL_MAIN_CONF    = 0x00100000; // mail
    public const uint NGX_MAIL_SRV_CONF     = 0x00200000; // mail > server
    public const uint NGX_STREAM_MAIN_CONF  = 0x00400000; // stream
    public const uint NGX_STREAM_SRV_CONF   = 0x00800000; // stream > server
    public const uint NGX_STREAM_UPS_CONF   = 0x01000000; // stream > upstream
    public const uint NGX_HTTP_MAIN_CONF    = 0x02000000; // http
    public const uint NGX_HTTP_SRV_CONF     = 0x04000000; // http > server
    public const uint NGX_HTTP_LOC_CONF     = 0x08000000; // http > location
    public const uint NGX_HTTP_UPS_CONF     = 0x10000000; // http > upstream
    public const uint NGX_HTTP_SIF_CONF      = 0x20000000; // http > server > if
    public const uint NGX_HTTP_LIF_CONF      = 0x40000000; // http > location > if
    public const uint NGX_HTTP_LMT_CONF      = 0x80000000; // http > location > limit_except
}