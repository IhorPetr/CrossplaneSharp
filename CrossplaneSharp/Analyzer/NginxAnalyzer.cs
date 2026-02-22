using CrossplaneSharp.Exceptions;

namespace CrossplaneSharp;

/// <summary>
/// Validates nginx directives: context and argument-count.
/// Faithful C# port of Python crossplane <c>analyzer.py</c>.
/// </summary>
public static class NginxAnalyzer
{
    // ─── bitmasks for argument styles ────────────────────────────────────────
    public const uint NGX_CONF_NOARGS = 0x00000001;
    public const uint NGX_CONF_TAKE1  = 0x00000002;
    public const uint NGX_CONF_TAKE2  = 0x00000004;
    public const uint NGX_CONF_TAKE3  = 0x00000008;
    public const uint NGX_CONF_TAKE4  = 0x00000010;
    public const uint NGX_CONF_TAKE5  = 0x00000020;
    public const uint NGX_CONF_TAKE6  = 0x00000040;
    public const uint NGX_CONF_TAKE7  = 0x00000080;
    public const uint NGX_CONF_BLOCK  = 0x00000100;
    public const uint NGX_CONF_FLAG   = 0x00000200;
    public const uint NGX_CONF_ANY    = 0x00000400;
    public const uint NGX_CONF_1MORE  = 0x00000800;
    public const uint NGX_CONF_2MORE  = 0x00001000;
    public const uint NGX_CONF_TAKE12   = NGX_CONF_TAKE1 | NGX_CONF_TAKE2;
    public const uint NGX_CONF_TAKE13   = NGX_CONF_TAKE1 | NGX_CONF_TAKE3;
    public const uint NGX_CONF_TAKE23   = NGX_CONF_TAKE2 | NGX_CONF_TAKE3;
    public const uint NGX_CONF_TAKE34   = NGX_CONF_TAKE3 | NGX_CONF_TAKE4;
    public const uint NGX_CONF_TAKE123  = NGX_CONF_TAKE12 | NGX_CONF_TAKE3;
    public const uint NGX_CONF_TAKE1234 = NGX_CONF_TAKE123 | NGX_CONF_TAKE4;

    // ─── bitmasks for directive locations ────────────────────────────────────
    public const uint NGX_DIRECT_CONF      = 0x00010000;
    public const uint NGX_MAIN_CONF        = 0x00040000;
    public const uint NGX_EVENT_CONF       = 0x00080000;
    public const uint NGX_MAIL_MAIN_CONF   = 0x00100000;
    public const uint NGX_MAIL_SRV_CONF    = 0x00200000;
    public const uint NGX_STREAM_MAIN_CONF = 0x00400000;
    public const uint NGX_STREAM_SRV_CONF  = 0x00800000;
    public const uint NGX_STREAM_UPS_CONF  = 0x01000000;
    public const uint NGX_HTTP_MAIN_CONF   = 0x02000000;
    public const uint NGX_HTTP_SRV_CONF    = 0x04000000;
    public const uint NGX_HTTP_LOC_CONF    = 0x08000000;
    public const uint NGX_HTTP_UPS_CONF    = 0x10000000;
    public const uint NGX_HTTP_SIF_CONF    = 0x20000000;
    public const uint NGX_HTTP_LIF_CONF    = 0x40000000;
    public const uint NGX_HTTP_LMT_CONF    = 0x80000000;

    public const uint NGX_ANY_CONF =
        NGX_MAIN_CONF | NGX_EVENT_CONF |
        NGX_MAIL_MAIN_CONF | NGX_MAIL_SRV_CONF |
        NGX_STREAM_MAIN_CONF | NGX_STREAM_SRV_CONF | NGX_STREAM_UPS_CONF |
        NGX_HTTP_MAIN_CONF | NGX_HTTP_SRV_CONF | NGX_HTTP_LOC_CONF | NGX_HTTP_UPS_CONF;

    // ─── context map (mirrors Python CONTEXTS dict) ───────────────────────────
    /// <summary>Maps context tuple (joined with "|") → location bitmask.</summary>
    public static readonly IReadOnlyDictionary<string, uint> Contexts =
        new Dictionary<string, uint>
        {
            [""]                              = NGX_MAIN_CONF,
            ["events"]                        = NGX_EVENT_CONF,
            ["mail"]                          = NGX_MAIL_MAIN_CONF,
            ["mail|server"]                   = NGX_MAIL_SRV_CONF,
            ["stream"]                        = NGX_STREAM_MAIN_CONF,
            ["stream|server"]                 = NGX_STREAM_SRV_CONF,
            ["stream|upstream"]               = NGX_STREAM_UPS_CONF,
            ["http"]                          = NGX_HTTP_MAIN_CONF,
            ["http|server"]                   = NGX_HTTP_SRV_CONF,
            ["http|location"]                 = NGX_HTTP_LOC_CONF,
            ["http|upstream"]                 = NGX_HTTP_UPS_CONF,
            ["http|server|if"]                = NGX_HTTP_SIF_CONF,
            ["http|location|if"]              = NGX_HTTP_LIF_CONF,
            ["http|location|limit_except"]    = NGX_HTTP_LMT_CONF,
        };

    // ─── directive map  ───────────────────────────────────────────────────────
    /// <summary>
    /// Maps directive name → list of valid bitmasks.
    /// Mirrors Python DIRECTIVES dict. Each mask combines location + arg-style bits.
    /// </summary>
    public static readonly Dictionary<string, List<uint>> Directives =
        new(StringComparer.Ordinal)
        {
            ["absolute_redirect"]         = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["accept_mutex"]              = [NGX_EVENT_CONF|NGX_CONF_FLAG],
            ["accept_mutex_delay"]        = [NGX_EVENT_CONF|NGX_CONF_TAKE1],
            ["access_log"]                = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_HTTP_LMT_CONF|NGX_CONF_1MORE,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_1MORE],
            ["add_after_body"]            = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["add_before_body"]           = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["add_header"]                = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_TAKE23],
            ["add_trailer"]               = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_TAKE23],
            ["addition_types"]            = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["aio"]                       = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["aio_write"]                 = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["alias"]                     = [NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["allow"]                     = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LMT_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["ancient_browser"]           = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["ancient_browser_value"]     = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["auth_basic"]                = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LMT_CONF|NGX_CONF_TAKE1],
            ["auth_basic_user_file"]      = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LMT_CONF|NGX_CONF_TAKE1],
            ["auth_http"]                 = [NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1],
            ["auth_http_header"]          = [NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE2],
            ["auth_http_pass_client_cert"]= [NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_FLAG],
            ["auth_http_timeout"]         = [NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1],
            ["auth_request"]              = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["auth_request_set"]          = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE2],
            ["autoindex"]                 = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["autoindex_exact_size"]      = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["autoindex_format"]          = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["autoindex_localtime"]       = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["break"]                     = [NGX_HTTP_SRV_CONF|NGX_HTTP_SIF_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_NOARGS],
            ["charset"]                   = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_TAKE1],
            ["charset_map"]               = [NGX_HTTP_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE2],
            ["charset_types"]             = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["chunked_transfer_encoding"] = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["client_body_buffer_size"]   = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["client_body_in_file_only"]  = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["client_body_in_single_buffer"] = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["client_body_temp_path"]     = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1234],
            ["client_body_timeout"]       = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["client_header_buffer_size"] = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1],
            ["client_header_timeout"]     = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1],
            ["client_max_body_size"]      = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["connection_pool_size"]      = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1],
            ["create_full_put_path"]      = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["daemon"]                    = [NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_FLAG],
            ["dav_access"]                = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE123],
            ["dav_methods"]               = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["debug_connection"]          = [NGX_EVENT_CONF|NGX_CONF_TAKE1],
            ["debug_points"]              = [NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1],
            ["default_type"]              = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["deny"]                      = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LMT_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["directio"]                  = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["directio_alignment"]        = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["disable_symlinks"]          = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE12],
            ["empty_gif"]                 = [NGX_HTTP_LOC_CONF|NGX_CONF_NOARGS],
            ["env"]                       = [NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1],
            ["error_log"]                 = [NGX_MAIN_CONF|NGX_CONF_1MORE,
                                             NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_1MORE,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_1MORE],
            ["error_page"]                = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_2MORE],
            ["etag"]                      = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["events"]                    = [NGX_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_NOARGS],
            ["expires"]                   = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_TAKE12],
            ["fastcgi_bind"]              = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE12],
            ["fastcgi_buffer_size"]       = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["fastcgi_buffering"]         = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["fastcgi_buffers"]           = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE2],
            ["fastcgi_busy_buffers_size"] = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["fastcgi_cache"]             = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["fastcgi_cache_background_update"] = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["fastcgi_cache_bypass"]      = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["fastcgi_cache_key"]         = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["fastcgi_cache_lock"]        = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["fastcgi_cache_lock_age"]    = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["fastcgi_cache_lock_timeout"]= [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["fastcgi_cache_max_range_offset"] = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["fastcgi_cache_methods"]     = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["fastcgi_cache_min_uses"]    = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["fastcgi_cache_path"]        = [NGX_HTTP_MAIN_CONF|NGX_CONF_2MORE],
            ["fastcgi_cache_revalidate"]  = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["fastcgi_cache_use_stale"]   = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["fastcgi_cache_valid"]       = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["fastcgi_connect_timeout"]   = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["fastcgi_force_ranges"]      = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["fastcgi_hide_header"]       = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["fastcgi_ignore_client_abort"]= [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["fastcgi_ignore_headers"]    = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["fastcgi_index"]             = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["fastcgi_intercept_errors"]  = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["fastcgi_keep_conn"]         = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["fastcgi_limit_rate"]        = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["fastcgi_max_temp_file_size"]= [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["fastcgi_next_upstream"]     = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["fastcgi_next_upstream_timeout"] = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["fastcgi_next_upstream_tries"] = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["fastcgi_no_cache"]          = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["fastcgi_param"]             = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE23],
            ["fastcgi_pass"]              = [NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_TAKE1],
            ["fastcgi_pass_header"]       = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["fastcgi_pass_request_body"] = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["fastcgi_pass_request_headers"] = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["fastcgi_read_timeout"]      = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["fastcgi_request_buffering"] = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["fastcgi_send_lowat"]        = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["fastcgi_send_timeout"]      = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["fastcgi_socket_keepalive"]  = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["fastcgi_split_path_info"]   = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["fastcgi_store"]             = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["fastcgi_store_access"]      = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE123],
            ["fastcgi_temp_file_write_size"] = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["fastcgi_temp_path"]         = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1234],
            ["flv"]                       = [NGX_HTTP_LOC_CONF|NGX_CONF_NOARGS],
            ["geo"]                       = [NGX_HTTP_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE12,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE12],
            ["gzip"]                      = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_FLAG],
            ["gzip_buffers"]              = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE2],
            ["gzip_comp_level"]           = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["gzip_disable"]              = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["gzip_http_version"]         = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["gzip_min_length"]           = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["gzip_proxied"]              = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["gzip_static"]               = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["gzip_types"]                = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["gzip_vary"]                 = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["hash"]                      = [NGX_HTTP_UPS_CONF|NGX_CONF_TAKE12,
                                             NGX_STREAM_UPS_CONF|NGX_CONF_TAKE12],
            ["http"]                      = [NGX_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_NOARGS],
            ["if"]                        = [NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_BLOCK|NGX_CONF_1MORE],
            ["if_modified_since"]         = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["ignore_invalid_headers"]    = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_FLAG],
            ["include"]                   = [NGX_ANY_CONF|NGX_CONF_TAKE1],
            ["index"]                     = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["internal"]                  = [NGX_HTTP_LOC_CONF|NGX_CONF_NOARGS],
            ["ip_hash"]                   = [NGX_HTTP_UPS_CONF|NGX_CONF_NOARGS],
            ["keepalive"]                 = [NGX_HTTP_UPS_CONF|NGX_CONF_TAKE1],
            ["keepalive_disable"]         = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE12],
            ["keepalive_requests"]        = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1,
                                             NGX_HTTP_UPS_CONF|NGX_CONF_TAKE1],
            ["keepalive_timeout"]         = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE12,
                                             NGX_HTTP_UPS_CONF|NGX_CONF_TAKE1],
            ["large_client_header_buffers"] = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE2],
            ["least_conn"]                = [NGX_HTTP_UPS_CONF|NGX_CONF_NOARGS,
                                             NGX_STREAM_UPS_CONF|NGX_CONF_NOARGS],
            ["limit_conn"]                = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE2,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE2],
            ["limit_conn_log_level"]      = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["limit_conn_status"]         = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["limit_conn_zone"]           = [NGX_HTTP_MAIN_CONF|NGX_CONF_TAKE2,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_TAKE2],
            ["limit_except"]              = [NGX_HTTP_LOC_CONF|NGX_CONF_BLOCK|NGX_CONF_1MORE],
            ["limit_rate"]                = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_TAKE1],
            ["limit_rate_after"]          = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_TAKE1],
            ["limit_req"]                 = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE123],
            ["limit_req_log_level"]       = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["limit_req_status"]          = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["limit_req_zone"]            = [NGX_HTTP_MAIN_CONF|NGX_CONF_TAKE34],
            ["listen"]                    = [NGX_HTTP_SRV_CONF|NGX_CONF_1MORE,
                                             NGX_MAIL_SRV_CONF|NGX_CONF_1MORE,
                                             NGX_STREAM_SRV_CONF|NGX_CONF_1MORE],
            ["load_module"]               = [NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1],
            ["location"]                  = [NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE12],
            ["lock_file"]                 = [NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1],
            ["log_format"]                = [NGX_HTTP_MAIN_CONF|NGX_CONF_2MORE,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_2MORE],
            ["log_not_found"]             = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["log_subrequest"]            = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["mail"]                      = [NGX_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_NOARGS],
            ["map"]                       = [NGX_HTTP_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE2,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE2],
            ["map_hash_bucket_size"]      = [NGX_HTTP_MAIN_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_TAKE1],
            ["map_hash_max_size"]         = [NGX_HTTP_MAIN_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_TAKE1],
            ["master_process"]            = [NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_FLAG],
            ["max_ranges"]                = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["merge_slashes"]             = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_FLAG],
            ["mirror"]                    = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["mirror_request_body"]       = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["mp4"]                       = [NGX_HTTP_LOC_CONF|NGX_CONF_NOARGS],
            ["mp4_buffer_size"]           = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["mp4_max_buffer_size"]       = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["multi_accept"]              = [NGX_EVENT_CONF|NGX_CONF_FLAG],
            ["open_file_cache"]           = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE12],
            ["open_file_cache_errors"]    = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["open_file_cache_min_uses"]  = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["open_file_cache_valid"]     = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["pcre_jit"]                  = [NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_FLAG],
            ["pid"]                       = [NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1],
            ["port_in_redirect"]          = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["postpone_output"]           = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["proxy_bind"]                = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE12,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE12],
            ["proxy_buffer_size"]         = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["proxy_buffering"]           = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["proxy_buffers"]             = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE2],
            ["proxy_busy_buffers_size"]   = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["proxy_cache"]               = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["proxy_cache_bypass"]        = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["proxy_cache_key"]           = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["proxy_cache_lock"]          = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["proxy_cache_methods"]       = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["proxy_cache_min_uses"]      = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["proxy_cache_path"]          = [NGX_HTTP_MAIN_CONF|NGX_CONF_2MORE],
            ["proxy_cache_revalidate"]    = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["proxy_cache_use_stale"]     = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["proxy_cache_valid"]         = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["proxy_connect_timeout"]     = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["proxy_cookie_domain"]       = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE12],
            ["proxy_cookie_path"]         = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE12],
            ["proxy_force_ranges"]        = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["proxy_hide_header"]         = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["proxy_http_version"]        = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["proxy_ignore_client_abort"] = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["proxy_ignore_headers"]      = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["proxy_intercept_errors"]    = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["proxy_next_upstream"]       = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_FLAG],
            ["proxy_next_upstream_timeout"] = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["proxy_next_upstream_tries"] = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["proxy_no_cache"]            = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["proxy_pass"]                = [NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_HTTP_LMT_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["proxy_pass_error_message"]  = [NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_FLAG],
            ["proxy_pass_header"]         = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["proxy_pass_request_body"]   = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["proxy_pass_request_headers"]= [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["proxy_protocol"]            = [NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_FLAG],
            ["proxy_read_timeout"]        = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["proxy_redirect"]            = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE12],
            ["proxy_request_buffering"]   = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["proxy_send_lowat"]          = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["proxy_send_timeout"]        = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["proxy_set_body"]            = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["proxy_set_header"]          = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE2],
            ["proxy_socket_keepalive"]    = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_FLAG],
            ["proxy_ssl_certificate"]     = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["proxy_ssl_certificate_key"] = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["proxy_ssl_ciphers"]         = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["proxy_ssl_protocols"]       = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_1MORE],
            ["proxy_ssl_server_name"]     = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_FLAG],
            ["proxy_ssl_verify"]          = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_FLAG],
            ["proxy_store"]               = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["proxy_temp_path"]           = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1234],
            ["proxy_timeout"]             = [NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["random_index"]              = [NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["read_ahead"]                = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["real_ip_header"]            = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["real_ip_recursive"]         = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["recursive_error_pages"]     = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["request_pool_size"]         = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1],
            ["reset_timedout_connection"] = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["resolver"]                  = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_1MORE,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_1MORE],
            ["resolver_timeout"]          = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["return"]                    = [NGX_HTTP_SRV_CONF|NGX_HTTP_SIF_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_TAKE12,
                                             NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["rewrite"]                   = [NGX_HTTP_SRV_CONF|NGX_HTTP_SIF_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_TAKE23],
            ["rewrite_log"]               = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_SIF_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_FLAG],
            ["root"]                      = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_TAKE1],
            ["satisfy"]                   = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["send_lowat"]                = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["send_timeout"]              = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["sendfile"]                  = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_FLAG],
            ["sendfile_max_chunk"]        = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["server"]                    = [NGX_HTTP_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_NOARGS,
                                             NGX_HTTP_UPS_CONF|NGX_CONF_1MORE,
                                             NGX_MAIL_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_NOARGS,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_NOARGS,
                                             NGX_STREAM_UPS_CONF|NGX_CONF_1MORE],
            ["server_name"]               = [NGX_HTTP_SRV_CONF|NGX_CONF_1MORE,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1],
            ["server_name_in_redirect"]   = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["server_names_hash_bucket_size"] = [NGX_HTTP_MAIN_CONF|NGX_CONF_TAKE1],
            ["server_names_hash_max_size"]= [NGX_HTTP_MAIN_CONF|NGX_CONF_TAKE1],
            ["server_tokens"]             = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["set"]                       = [NGX_HTTP_SRV_CONF|NGX_HTTP_SIF_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_TAKE2],
            ["set_real_ip_from"]          = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["slice"]                     = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["split_clients"]             = [NGX_HTTP_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE2,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE2],
            ["ssi"]                       = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_FLAG],
            ["ssi_last_modified"]         = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["ssi_silent_errors"]         = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["ssi_types"]                 = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["ssl"]                       = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_FLAG,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_FLAG],
            ["ssl_buffer_size"]           = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1],
            ["ssl_certificate"]           = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["ssl_certificate_key"]       = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["ssl_ciphers"]               = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["ssl_client_certificate"]    = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["ssl_dhparam"]               = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["ssl_prefer_server_ciphers"] = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_FLAG,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_FLAG,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_FLAG],
            ["ssl_protocols"]             = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_1MORE,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_1MORE,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_1MORE],
            ["ssl_session_cache"]         = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE12,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE12,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE12],
            ["ssl_session_tickets"]       = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_FLAG,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_FLAG,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_FLAG],
            ["ssl_session_timeout"]       = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["ssl_stapling"]              = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_FLAG],
            ["ssl_stapling_file"]         = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1],
            ["ssl_stapling_responder"]    = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1],
            ["ssl_stapling_verify"]       = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_FLAG],
            ["ssl_trusted_certificate"]   = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["ssl_verify_client"]         = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["ssl_verify_depth"]          = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1],
            ["stream"]                    = [NGX_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_NOARGS],
            ["stub_status"]               = [NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_NOARGS|NGX_CONF_TAKE1],
            ["sub_filter"]                = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE2],
            ["sub_filter_once"]           = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["sub_filter_types"]          = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["tcp_nodelay"]               = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_FLAG],
            ["tcp_nopush"]                = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG],
            ["thread_pool"]               = [NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE23],
            ["timer_resolution"]          = [NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1],
            ["try_files"]                 = [NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_2MORE],
            ["types"]                     = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_BLOCK|NGX_CONF_NOARGS],
            ["types_hash_bucket_size"]    = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["types_hash_max_size"]       = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["underscores_in_headers"]    = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_FLAG],
            ["upstream"]                  = [NGX_HTTP_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE1],
            ["use"]                       = [NGX_EVENT_CONF|NGX_CONF_TAKE1],
            ["user"]                      = [NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE12],
            ["userid"]                    = [NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1],
            ["valid_referers"]            = [NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE],
            ["variables_hash_bucket_size"]= [NGX_HTTP_MAIN_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_TAKE1],
            ["variables_hash_max_size"]   = [NGX_HTTP_MAIN_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_TAKE1],
            ["worker_aio_requests"]       = [NGX_EVENT_CONF|NGX_CONF_TAKE1],
            ["worker_connections"]        = [NGX_EVENT_CONF|NGX_CONF_TAKE1],
            ["worker_cpu_affinity"]       = [NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_1MORE],
            ["worker_priority"]           = [NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1],
            ["worker_processes"]          = [NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1],
            ["worker_rlimit_core"]        = [NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1],
            ["worker_rlimit_nofile"]      = [NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1],
            ["worker_shutdown_timeout"]   = [NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1],
            ["working_directory"]         = [NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1],
            ["zone"]                      = [NGX_HTTP_UPS_CONF|NGX_CONF_TAKE12,
                                             NGX_STREAM_UPS_CONF|NGX_CONF_TAKE12],
            // nginx+ directives
            ["health_check"]              = [NGX_HTTP_LOC_CONF|NGX_CONF_ANY,
                                             NGX_STREAM_SRV_CONF|NGX_CONF_ANY],
            ["least_time"]                = [NGX_HTTP_UPS_CONF|NGX_CONF_TAKE12,
                                             NGX_STREAM_UPS_CONF|NGX_CONF_TAKE12],
            ["match"]                     = [NGX_HTTP_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE1],
            ["status"]                    = [NGX_HTTP_LOC_CONF|NGX_CONF_NOARGS],
            ["sticky"]                    = [NGX_HTTP_UPS_CONF|NGX_CONF_1MORE],
        };

    // ─── External directive registration (mirrors Python's register_external_directives) ──
    public static void RegisterDirectives(Dictionary<string, List<uint>> directives)
    {
        foreach (var (name, masks) in directives)
            if (masks.Count > 0)
                Directives[name] = masks;
    }

    // ─── Context key helper ───────────────────────────────────────────────────
    public static string ContextKey(IReadOnlyList<string> ctx) =>
        string.Join("|", ctx);

    /// <summary>
    /// Computes the child context after entering a block directive.
    /// Mirrors Python <c>enter_block_ctx</c>.
    /// </summary>
    public static IReadOnlyList<string> EnterBlockCtx(string directive, IReadOnlyList<string> ctx)
    {
        // location blocks inside http always normalise to ["http","location"]
        if (ctx.Count > 0 && ctx[0] == "http" && directive == "location")
            return ["http", "location"];

        var next = new List<string>(ctx) { directive };
        return next;
    }

    // ─── analyze() ───────────────────────────────────────────────────────────
    /// <summary>
    /// Validates <paramref name="directive"/> in <paramref name="ctx"/>.
    /// Mirrors Python <c>analyze()</c>.
    /// </summary>
    public static void Analyze(
        string? fname,
        string directive,
        int line,
        IReadOnlyList<string> args,
        string term,              // ";" or "{"
        IReadOnlyList<string> ctx,
        bool strict = false,
        bool checkCtx = true,
        bool checkArgs = true)
    {
        // strict mode: unknown directive → error
        if (strict && !Directives.ContainsKey(directive))
            throw new NgxParserDirectiveUnknownError(
                $"unknown directive \"{directive}\"", fname, line);

        string ctxKey = ContextKey(ctx);

        // skip analysis if context or directive is unknown
        if (!Contexts.ContainsKey(ctxKey) || !Directives.ContainsKey(directive))
            return;

        var masks = new List<uint>(Directives[directive]);
        uint ctxMask = Contexts[ctxKey];

        // context check
        if (checkCtx)
        {
            masks = masks.Where(m => (m & ctxMask) != 0).ToList();
            if (masks.Count == 0)
                throw new NgxParserDirectiveContextError(
                    $"\"{directive}\" directive is not allowed here", fname, line);
        }

        if (!checkArgs) return;

        int nArgs = args.Count;
        bool ValidFlag(string x) => x.Equals("on", StringComparison.OrdinalIgnoreCase)
                                 || x.Equals("off", StringComparison.OrdinalIgnoreCase);

        string? reason = null;
        foreach (var mask in Enumerable.Reverse(masks))
        {
            if ((mask & NGX_CONF_BLOCK) != 0 && term != "{")
            {
                reason = $"directive \"{directive}\" has no opening \"{{\"";
                continue;
            }
            if ((mask & NGX_CONF_BLOCK) == 0 && term != ";")
            {
                reason = $"directive \"{directive}\" is not terminated by \";\"";
                continue;
            }

            bool ok =
                ((mask >> nArgs & 1) != 0 && nArgs <= 7) ||
                ((mask & NGX_CONF_FLAG) != 0 && nArgs == 1 && ValidFlag(args[0])) ||
                ((mask & NGX_CONF_ANY)  != 0 && nArgs >= 0) ||
                ((mask & NGX_CONF_1MORE)!= 0 && nArgs >= 1) ||
                ((mask & NGX_CONF_2MORE)!= 0 && nArgs >= 2);

            if (ok) return;

            if ((mask & NGX_CONF_FLAG) != 0 && nArgs == 1 && !ValidFlag(args[0]))
                reason = $"invalid value \"{args[0]}\" in \"{directive}\" directive, it must be \"on\" or \"off\"";
            else
                reason = $"invalid number of arguments in \"{directive}\" directive";
        }

        throw new NgxParserDirectiveArgumentsError(reason ?? "invalid directive", fname, line);
    }
}

