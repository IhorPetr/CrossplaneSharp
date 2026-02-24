using System;
using System.Collections.Generic;
using System.Linq;
using CrossplaneSharp.Exceptions;

namespace CrossplaneSharp
{

/// <summary>
/// Validates nginx directives: context and argument-count.
/// C# port of Python crossplane <c>analyzer.py</c>.
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
    public static readonly Dictionary<string, uint[]> Directives =
        new Dictionary<string, uint[]>(StringComparer.Ordinal)
        {
            ["absolute_redirect"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["accept_mutex"]              = new uint[] { NGX_EVENT_CONF|NGX_CONF_FLAG },
            ["accept_mutex_delay"]        = new uint[] { NGX_EVENT_CONF|NGX_CONF_TAKE1 },
            ["access_log"]                = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_HTTP_LMT_CONF|NGX_CONF_1MORE,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_1MORE },
            ["add_after_body"]            = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["add_before_body"]           = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["add_header"]                = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_TAKE23 },
            ["add_trailer"]               = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_TAKE23 },
            ["addition_types"]            = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["aio"]                       = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["aio_write"]                 = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["alias"]                     = new uint[] { NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["allow"]                     = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LMT_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["ancient_browser"]           = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["ancient_browser_value"]     = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["auth_basic"]                = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LMT_CONF|NGX_CONF_TAKE1 },
            ["auth_basic_user_file"]      = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LMT_CONF|NGX_CONF_TAKE1 },
            ["auth_http"]                 = new uint[] { NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1 },
            ["auth_http_header"]          = new uint[] { NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE2 },
            ["auth_http_pass_client_cert"]= new uint[] { NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_FLAG },
            ["auth_http_timeout"]         = new uint[] { NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1 },
            ["auth_request"]              = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["auth_request_set"]          = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE2 },
            ["autoindex"]                 = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["autoindex_exact_size"]      = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["autoindex_format"]          = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["autoindex_localtime"]       = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["break"]                     = new uint[] { NGX_HTTP_SRV_CONF|NGX_HTTP_SIF_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_NOARGS },
            ["charset"]                   = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_TAKE1 },
            ["charset_map"]               = new uint[] { NGX_HTTP_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE2 },
            ["charset_types"]             = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["chunked_transfer_encoding"] = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["client_body_buffer_size"]   = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["client_body_in_file_only"]  = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["client_body_in_single_buffer"] = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["client_body_temp_path"]     = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1234 },
            ["client_body_timeout"]       = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["client_header_buffer_size"] = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1 },
            ["client_header_timeout"]     = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1 },
            ["client_max_body_size"]      = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["connection_pool_size"]      = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1 },
            ["create_full_put_path"]      = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["daemon"]                    = new uint[] { NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_FLAG },
            ["dav_access"]                = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE123 },
            ["dav_methods"]               = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["debug_connection"]          = new uint[] { NGX_EVENT_CONF|NGX_CONF_TAKE1 },
            ["debug_points"]              = new uint[] { NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1 },
            ["default_type"]              = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["deny"]                      = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LMT_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["directio"]                  = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["directio_alignment"]        = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["disable_symlinks"]          = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE12 },
            ["empty_gif"]                 = new uint[] { NGX_HTTP_LOC_CONF|NGX_CONF_NOARGS },
            ["env"]                       = new uint[] { NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1 },
            ["error_log"]                 = new uint[] { NGX_MAIN_CONF|NGX_CONF_1MORE,
                                             NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_1MORE,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_1MORE },
            ["error_page"]                = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_2MORE },
            ["etag"]                      = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["events"]                    = new uint[] { NGX_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_NOARGS },
            ["expires"]                   = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_TAKE12 },
            ["fastcgi_bind"]              = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE12 },
            ["fastcgi_buffer_size"]       = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["fastcgi_buffering"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["fastcgi_buffers"]           = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE2 },
            ["fastcgi_busy_buffers_size"] = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["fastcgi_cache"]             = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["fastcgi_cache_background_update"] = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["fastcgi_cache_bypass"]      = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["fastcgi_cache_key"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["fastcgi_cache_lock"]        = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["fastcgi_cache_lock_age"]    = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["fastcgi_cache_lock_timeout"]= new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["fastcgi_cache_max_range_offset"] = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["fastcgi_cache_methods"]     = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["fastcgi_cache_min_uses"]    = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["fastcgi_cache_path"]        = new uint[] { NGX_HTTP_MAIN_CONF|NGX_CONF_2MORE },
            ["fastcgi_cache_revalidate"]  = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["fastcgi_cache_use_stale"]   = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["fastcgi_cache_valid"]       = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["fastcgi_connect_timeout"]   = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["fastcgi_force_ranges"]      = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["fastcgi_hide_header"]       = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["fastcgi_ignore_client_abort"]= new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["fastcgi_ignore_headers"]    = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["fastcgi_index"]             = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["fastcgi_intercept_errors"]  = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["fastcgi_keep_conn"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["fastcgi_limit_rate"]        = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["fastcgi_max_temp_file_size"]= new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["fastcgi_next_upstream"]     = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["fastcgi_next_upstream_timeout"] = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["fastcgi_next_upstream_tries"] = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["fastcgi_no_cache"]          = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["fastcgi_param"]             = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE23 },
            ["fastcgi_pass"]              = new uint[] { NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_TAKE1 },
            ["fastcgi_pass_header"]       = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["fastcgi_pass_request_body"] = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["fastcgi_pass_request_headers"] = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["fastcgi_read_timeout"]      = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["fastcgi_request_buffering"] = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["fastcgi_send_lowat"]        = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["fastcgi_send_timeout"]      = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["fastcgi_socket_keepalive"]  = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["fastcgi_split_path_info"]   = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["fastcgi_store"]             = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["fastcgi_store_access"]      = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE123 },
            ["fastcgi_temp_file_write_size"] = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["fastcgi_temp_path"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1234 },
            ["flv"]                       = new uint[] { NGX_HTTP_LOC_CONF|NGX_CONF_NOARGS },
            ["geo"]                       = new uint[] { NGX_HTTP_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE12,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE12 },
            ["gzip"]                      = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_FLAG },
            ["gzip_buffers"]              = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE2 },
            ["gzip_comp_level"]           = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["gzip_disable"]              = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["gzip_http_version"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["gzip_min_length"]           = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["gzip_proxied"]              = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["gzip_static"]               = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["gzip_types"]                = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["gzip_vary"]                 = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["hash"]                      = new uint[] { NGX_HTTP_UPS_CONF|NGX_CONF_TAKE12,
                                             NGX_STREAM_UPS_CONF|NGX_CONF_TAKE12 },
            ["http"]                      = new uint[] { NGX_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_NOARGS },
            ["if"]                        = new uint[] { NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_BLOCK|NGX_CONF_1MORE },
            ["if_modified_since"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["ignore_invalid_headers"]    = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_FLAG },
            ["include"]                   = new uint[] { NGX_ANY_CONF|NGX_CONF_TAKE1 },
            ["index"]                     = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["internal"]                  = new uint[] { NGX_HTTP_LOC_CONF|NGX_CONF_NOARGS },
            ["ip_hash"]                   = new uint[] { NGX_HTTP_UPS_CONF|NGX_CONF_NOARGS },
            ["keepalive"]                 = new uint[] { NGX_HTTP_UPS_CONF|NGX_CONF_TAKE1 },
            ["keepalive_disable"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE12 },
            ["keepalive_requests"]        = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1,
                                             NGX_HTTP_UPS_CONF|NGX_CONF_TAKE1 },
            ["keepalive_timeout"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE12,
                                             NGX_HTTP_UPS_CONF|NGX_CONF_TAKE1 },
            ["large_client_header_buffers"] = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE2 },
            ["least_conn"]                = new uint[] { NGX_HTTP_UPS_CONF|NGX_CONF_NOARGS,
                                             NGX_STREAM_UPS_CONF|NGX_CONF_NOARGS },
            ["limit_conn"]                = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE2,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE2 },
            ["limit_conn_log_level"]      = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["limit_conn_status"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["limit_conn_zone"]           = new uint[] { NGX_HTTP_MAIN_CONF|NGX_CONF_TAKE2,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_TAKE2 },
            ["limit_except"]              = new uint[] { NGX_HTTP_LOC_CONF|NGX_CONF_BLOCK|NGX_CONF_1MORE },
            ["limit_rate"]                = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_TAKE1 },
            ["limit_rate_after"]          = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_TAKE1 },
            ["limit_req"]                 = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE123 },
            ["limit_req_log_level"]       = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["limit_req_status"]          = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["limit_req_zone"]            = new uint[] { NGX_HTTP_MAIN_CONF|NGX_CONF_TAKE34 },
            ["listen"]                    = new uint[] { NGX_HTTP_SRV_CONF|NGX_CONF_1MORE,
                                             NGX_MAIL_SRV_CONF|NGX_CONF_1MORE,
                                             NGX_STREAM_SRV_CONF|NGX_CONF_1MORE },
            ["load_module"]               = new uint[] { NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1 },
            ["location"]                  = new uint[] { NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE12 },
            ["lock_file"]                 = new uint[] { NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1 },
            ["log_format"]                = new uint[] { NGX_HTTP_MAIN_CONF|NGX_CONF_2MORE,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_2MORE },
            ["log_not_found"]             = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["log_subrequest"]            = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["mail"]                      = new uint[] { NGX_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_NOARGS },
            ["map"]                       = new uint[] { NGX_HTTP_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE2,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE2 },
            ["map_hash_bucket_size"]      = new uint[] { NGX_HTTP_MAIN_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_TAKE1 },
            ["map_hash_max_size"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_TAKE1 },
            ["master_process"]            = new uint[] { NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_FLAG },
            ["max_ranges"]                = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["merge_slashes"]             = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_FLAG },
            ["mirror"]                    = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["mirror_request_body"]       = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["mp4"]                       = new uint[] { NGX_HTTP_LOC_CONF|NGX_CONF_NOARGS },
            ["mp4_buffer_size"]           = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["mp4_max_buffer_size"]       = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["multi_accept"]              = new uint[] { NGX_EVENT_CONF|NGX_CONF_FLAG },
            ["open_file_cache"]           = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE12 },
            ["open_file_cache_errors"]    = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["open_file_cache_min_uses"]  = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["open_file_cache_valid"]     = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["pcre_jit"]                  = new uint[] { NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_FLAG },
            ["pid"]                       = new uint[] { NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1 },
            ["port_in_redirect"]          = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["postpone_output"]           = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["proxy_bind"]                = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE12,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE12 },
            ["proxy_buffer_size"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["proxy_buffering"]           = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["proxy_buffers"]             = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE2 },
            ["proxy_busy_buffers_size"]   = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["proxy_cache"]               = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["proxy_cache_bypass"]        = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["proxy_cache_key"]           = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["proxy_cache_lock"]          = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["proxy_cache_methods"]       = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["proxy_cache_min_uses"]      = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["proxy_cache_path"]          = new uint[] { NGX_HTTP_MAIN_CONF|NGX_CONF_2MORE },
            ["proxy_cache_revalidate"]    = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["proxy_cache_use_stale"]     = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["proxy_cache_valid"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["proxy_connect_timeout"]     = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["proxy_cookie_domain"]       = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE12 },
            ["proxy_cookie_path"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE12 },
            ["proxy_force_ranges"]        = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["proxy_hide_header"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["proxy_http_version"]        = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["proxy_ignore_client_abort"] = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["proxy_ignore_headers"]      = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["proxy_intercept_errors"]    = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["proxy_next_upstream"]       = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_FLAG },
            ["proxy_next_upstream_timeout"] = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["proxy_next_upstream_tries"] = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["proxy_no_cache"]            = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["proxy_pass"]                = new uint[] { NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_HTTP_LMT_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["proxy_pass_error_message"]  = new uint[] { NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_FLAG },
            ["proxy_pass_header"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["proxy_pass_request_body"]   = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["proxy_pass_request_headers"]= new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["proxy_protocol"]            = new uint[] { NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_FLAG },
            ["proxy_read_timeout"]        = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["proxy_redirect"]            = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE12 },
            ["proxy_request_buffering"]   = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["proxy_send_lowat"]          = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["proxy_send_timeout"]        = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["proxy_set_body"]            = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["proxy_set_header"]          = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE2 },
            ["proxy_socket_keepalive"]    = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_FLAG },
            ["proxy_ssl_certificate"]     = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["proxy_ssl_certificate_key"] = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["proxy_ssl_ciphers"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["proxy_ssl_protocols"]       = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_1MORE },
            ["proxy_ssl_server_name"]     = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_FLAG },
            ["proxy_ssl_verify"]          = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_FLAG },
            ["proxy_store"]               = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["proxy_temp_path"]           = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1234 },
            ["proxy_timeout"]             = new uint[] { NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["random_index"]              = new uint[] { NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["read_ahead"]                = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["real_ip_header"]            = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["real_ip_recursive"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["recursive_error_pages"]     = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["request_pool_size"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1 },
            ["reset_timedout_connection"] = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["resolver"]                  = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_1MORE,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_1MORE },
            ["resolver_timeout"]          = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["return"]                    = new uint[] { NGX_HTTP_SRV_CONF|NGX_HTTP_SIF_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_TAKE12,
                                             NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["rewrite"]                   = new uint[] { NGX_HTTP_SRV_CONF|NGX_HTTP_SIF_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_TAKE23 },
            ["rewrite_log"]               = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_SIF_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_FLAG },
            ["root"]                      = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_TAKE1 },
            ["satisfy"]                   = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["send_lowat"]                = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["send_timeout"]              = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["sendfile"]                  = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_FLAG },
            ["sendfile_max_chunk"]        = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["server"]                    = new uint[] { NGX_HTTP_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_NOARGS,
                                             NGX_HTTP_UPS_CONF|NGX_CONF_1MORE,
                                             NGX_MAIL_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_NOARGS,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_NOARGS,
                                             NGX_STREAM_UPS_CONF|NGX_CONF_1MORE },
            ["server_name"]               = new uint[] { NGX_HTTP_SRV_CONF|NGX_CONF_1MORE,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1 },
            ["server_name_in_redirect"]   = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["server_names_hash_bucket_size"] = new uint[] { NGX_HTTP_MAIN_CONF|NGX_CONF_TAKE1 },
            ["server_names_hash_max_size"]= new uint[] { NGX_HTTP_MAIN_CONF|NGX_CONF_TAKE1 },
            ["server_tokens"]             = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["set"]                       = new uint[] { NGX_HTTP_SRV_CONF|NGX_HTTP_SIF_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_TAKE2 },
            ["set_real_ip_from"]          = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["slice"]                     = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["split_clients"]             = new uint[] { NGX_HTTP_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE2,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE2 },
            ["ssi"]                       = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_HTTP_LIF_CONF|NGX_CONF_FLAG },
            ["ssi_last_modified"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["ssi_silent_errors"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["ssi_types"]                 = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["ssl"]                       = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_FLAG,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_FLAG },
            ["ssl_buffer_size"]           = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1 },
            ["ssl_certificate"]           = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["ssl_certificate_key"]       = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["ssl_ciphers"]               = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["ssl_client_certificate"]    = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["ssl_dhparam"]               = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["ssl_prefer_server_ciphers"] = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_FLAG,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_FLAG,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_FLAG },
            ["ssl_protocols"]             = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_1MORE,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_1MORE,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_1MORE },
            ["ssl_session_cache"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE12,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE12,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE12 },
            ["ssl_session_tickets"]       = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_FLAG,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_FLAG,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_FLAG },
            ["ssl_session_timeout"]       = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["ssl_stapling"]              = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_FLAG },
            ["ssl_stapling_file"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1 },
            ["ssl_stapling_responder"]    = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1 },
            ["ssl_stapling_verify"]       = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_FLAG },
            ["ssl_trusted_certificate"]   = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["ssl_verify_client"]         = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["ssl_verify_depth"]          = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_MAIL_MAIN_CONF|NGX_MAIL_SRV_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_TAKE1 },
            ["stream"]                    = new uint[] { NGX_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_NOARGS },
            ["stub_status"]               = new uint[] { NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_NOARGS|NGX_CONF_TAKE1 },
            ["sub_filter"]                = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE2 },
            ["sub_filter_once"]           = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["sub_filter_types"]          = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["tcp_nodelay"]               = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG,
                                             NGX_STREAM_MAIN_CONF|NGX_STREAM_SRV_CONF|NGX_CONF_FLAG },
            ["tcp_nopush"]                = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_FLAG },
            ["thread_pool"]               = new uint[] { NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE23 },
            ["timer_resolution"]          = new uint[] { NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1 },
            ["try_files"]                 = new uint[] { NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_2MORE },
            ["types"]                     = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_BLOCK|NGX_CONF_NOARGS },
            ["types_hash_bucket_size"]    = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["types_hash_max_size"]       = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["underscores_in_headers"]    = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_CONF_FLAG },
            ["upstream"]                  = new uint[] { NGX_HTTP_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE1 },
            ["use"]                       = new uint[] { NGX_EVENT_CONF|NGX_CONF_TAKE1 },
            ["user"]                      = new uint[] { NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE12 },
            ["userid"]                    = new uint[] { NGX_HTTP_MAIN_CONF|NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_TAKE1 },
            ["valid_referers"]            = new uint[] { NGX_HTTP_SRV_CONF|NGX_HTTP_LOC_CONF|NGX_CONF_1MORE },
            ["variables_hash_bucket_size"]= new uint[] { NGX_HTTP_MAIN_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_TAKE1 },
            ["variables_hash_max_size"]   = new uint[] { NGX_HTTP_MAIN_CONF|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_TAKE1 },
            ["worker_aio_requests"]       = new uint[] { NGX_EVENT_CONF|NGX_CONF_TAKE1 },
            ["worker_connections"]        = new uint[] { NGX_EVENT_CONF|NGX_CONF_TAKE1 },
            ["worker_cpu_affinity"]       = new uint[] { NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_1MORE },
            ["worker_priority"]           = new uint[] { NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1 },
            ["worker_processes"]          = new uint[] { NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1 },
            ["worker_rlimit_core"]        = new uint[] { NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1 },
            ["worker_rlimit_nofile"]      = new uint[] { NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1 },
            ["worker_shutdown_timeout"]   = new uint[] { NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1 },
            ["working_directory"]         = new uint[] { NGX_MAIN_CONF|NGX_DIRECT_CONF|NGX_CONF_TAKE1 },
            ["zone"]                      = new uint[] { NGX_HTTP_UPS_CONF|NGX_CONF_TAKE12,
                                             NGX_STREAM_UPS_CONF|NGX_CONF_TAKE12 },
            // nginx+ directives
            ["health_check"]              = new uint[] { NGX_HTTP_LOC_CONF|NGX_CONF_ANY,
                                             NGX_STREAM_SRV_CONF|NGX_CONF_ANY },
            ["least_time"]                = new uint[] { NGX_HTTP_UPS_CONF|NGX_CONF_TAKE12,
                                             NGX_STREAM_UPS_CONF|NGX_CONF_TAKE12 },
            ["match"]                     = new uint[] { NGX_HTTP_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE1,
                                             NGX_STREAM_MAIN_CONF|NGX_CONF_BLOCK|NGX_CONF_TAKE1 },
            ["status"]                    = new uint[] { NGX_HTTP_LOC_CONF|NGX_CONF_NOARGS },
            ["sticky"]                    = new uint[] { NGX_HTTP_UPS_CONF|NGX_CONF_1MORE },
        };

    // ─── External directive registration (mirrors Python's register_external_directives) ──
    public static void RegisterDirectives(Dictionary<string, uint[]> directives)
    {
        foreach (var kvp in directives)
            if (kvp.Value.Length > 0)
                Directives[kvp.Key] = kvp.Value;
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
            return new List<string> { "http", "location" };

        var next = new List<string>(ctx) { directive };
        return next;
    }

    // ─── analyze() ───────────────────────────────────────────────────────────
    /// <summary>
    /// Validates <paramref name="directive"/> in <paramref name="ctx"/>.
    /// Mirrors Python <c>analyze()</c>.
    /// </summary>
    public static void Analyze(
        string fname,
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

        string reason = null;
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
}
