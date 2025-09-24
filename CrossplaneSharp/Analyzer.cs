namespace CrossplaneSharp;

public class Analyzer
{
    private readonly Dictionary<string, uint> _directives = new()
    {
        ["absolute_redirect"] = DirectiveLocations.NGX_HTTP_MAIN_CONF | DirectiveLocations.NGX_HTTP_SRV_CONF
                                                                      | DirectiveLocations.NGX_HTTP_LOC_CONF
                                                                      | DirectiveArgument.NGX_CONF_FLAG,
        ["accept_mutex"] = DirectiveLocations.NGX_EVENT_CONF | DirectiveArgument.NGX_CONF_FLAG,
        ["accept_mutex_delay"] = DirectiveLocations.NGX_EVENT_CONF | DirectiveArgument.NGX_CONF_TAKE1,
        ["access_log"] = DirectiveLocations.NGX_HTTP_MAIN_CONF | DirectiveLocations.NGX_HTTP_SRV_CONF
                                                               | DirectiveLocations.NGX_HTTP_LOC_CONF
                                                               | DirectiveLocations.NGX_HTTP_LIF_CONF
                                                               | DirectiveLocations.NGX_HTTP_LMT_CONF
                                                               | DirectiveArgument.NGX_CONF_1MORE
                                                               | DirectiveLocations.NGX_STREAM_MAIN_CONF
                                                               | DirectiveLocations.NGX_STREAM_SRV_CONF
                                                               | DirectiveArgument.NGX_CONF_1MORE,
        ["add_after_body"] = DirectiveLocations.NGX_HTTP_MAIN_CONF | DirectiveLocations.NGX_HTTP_SRV_CONF
                                                                   | DirectiveLocations.NGX_HTTP_LOC_CONF
                                                                   | DirectiveArgument.NGX_CONF_TAKE1,
        ["add_before_body"] = DirectiveLocations.NGX_HTTP_MAIN_CONF | DirectiveLocations.NGX_HTTP_SRV_CONF
                                                                    | DirectiveLocations.NGX_HTTP_LOC_CONF
                                                                    | DirectiveArgument.NGX_CONF_TAKE1,
        ["add_header"] = DirectiveLocations.NGX_HTTP_MAIN_CONF | DirectiveLocations.NGX_HTTP_SRV_CONF
                                                               | DirectiveLocations.NGX_HTTP_LOC_CONF
                                                               | DirectiveLocations.NGX_HTTP_LIF_CONF
                                                               | DirectiveArgument.NGX_CONF_TAKE23,
        ["add_trailer"] = DirectiveLocations.NGX_HTTP_MAIN_CONF | DirectiveLocations.NGX_HTTP_SRV_CONF
                                                                | DirectiveLocations.NGX_HTTP_LOC_CONF
                                                                | DirectiveLocations.NGX_HTTP_LIF_CONF
                                                                | DirectiveArgument.NGX_CONF_TAKE23,
        ["addition_types"] = DirectiveLocations.NGX_HTTP_MAIN_CONF | DirectiveLocations.NGX_HTTP_SRV_CONF
                                                                   | DirectiveLocations.NGX_HTTP_LOC_CONF
                                                                   | DirectiveArgument.NGX_CONF_1MORE,
        ["aio"] = DirectiveLocations.NGX_HTTP_MAIN_CONF | DirectiveLocations.NGX_HTTP_SRV_CONF
                                                        | DirectiveLocations.NGX_HTTP_LOC_CONF
                                                        | DirectiveArgument.NGX_CONF_TAKE1,
        ["aio_write"] = DirectiveLocations.NGX_HTTP_MAIN_CONF | DirectiveLocations.NGX_HTTP_SRV_CONF
                                                              | DirectiveLocations.NGX_HTTP_LOC_CONF
                                                              | DirectiveArgument.NGX_CONF_FLAG,
        ["alias"] = DirectiveLocations.NGX_HTTP_LOC_CONF | DirectiveArgument.NGX_CONF_TAKE1,
        ["allow"] = DirectiveLocations.NGX_HTTP_MAIN_CONF | DirectiveLocations.NGX_HTTP_SRV_CONF
                                                          | DirectiveLocations.NGX_HTTP_LOC_CONF
                                                          | DirectiveLocations.NGX_HTTP_LMT_CONF
                                                          | DirectiveArgument.NGX_CONF_TAKE1
                                                          | DirectiveLocations.NGX_STREAM_MAIN_CONF
                                                          | DirectiveLocations.NGX_STREAM_SRV_CONF
                                                          | DirectiveArgument.NGX_CONF_TAKE1,
        ["ancient_browser"] = DirectiveLocations.NGX_HTTP_MAIN_CONF | DirectiveLocations.NGX_HTTP_SRV_CONF
                                                                    | DirectiveLocations.NGX_HTTP_LOC_CONF
                                                                    | DirectiveArgument.NGX_CONF_1MORE,
        ["ancient_browser_value"] = DirectiveLocations.NGX_HTTP_MAIN_CONF | DirectiveLocations.NGX_HTTP_SRV_CONF
                                                                          | DirectiveLocations.NGX_HTTP_LOC_CONF
                                                                          | DirectiveArgument.NGX_CONF_TAKE1,
        ["auth_basic"] = DirectiveLocations.NGX_HTTP_MAIN_CONF | DirectiveLocations.NGX_HTTP_SRV_CONF
                                                               | DirectiveLocations.NGX_HTTP_LOC_CONF
                                                               | DirectiveLocations.NGX_HTTP_LMT_CONF
                                                               | DirectiveArgument.NGX_CONF_TAKE1,
        ["auth_basic_user_file"] = DirectiveLocations.NGX_HTTP_MAIN_CONF | DirectiveLocations.NGX_HTTP_SRV_CONF
                                                                         | DirectiveLocations.NGX_HTTP_LOC_CONF
                                                                         | DirectiveLocations.NGX_HTTP_LMT_CONF
                                                                         | DirectiveArgument.NGX_CONF_TAKE1,
        ["auth_http"] = DirectiveLocations.NGX_MAIL_MAIN_CONF | DirectiveLocations.NGX_MAIL_SRV_CONF
                                                              | DirectiveArgument.NGX_CONF_TAKE1,
        ["auth_http_header"] = DirectiveLocations.NGX_MAIL_MAIN_CONF | DirectiveLocations.NGX_MAIL_SRV_CONF
                                                                     | DirectiveArgument.NGX_CONF_TAKE2,
        ["auth_http_pass_client_cert"] = DirectiveLocations.NGX_MAIL_MAIN_CONF | DirectiveLocations.NGX_MAIL_SRV_CONF
                                                                               | DirectiveArgument.NGX_CONF_FLAG,
        ["auth_http_timeout"] = DirectiveLocations.NGX_MAIL_MAIN_CONF | DirectiveLocations.NGX_MAIL_SRV_CONF
                                                                      | DirectiveArgument.NGX_CONF_TAKE1,
    };

    private readonly Dictionary<uint, string[]> _context = new()
    {
        [DirectiveLocations.NGX_MAIN_CONF] = [],
        [DirectiveLocations.NGX_EVENT_CONF] = ["events"],
        [DirectiveLocations.NGX_MAIL_MAIN_CONF] = ["mail"],
        [DirectiveLocations.NGX_MAIL_SRV_CONF] = ["mail", "server"],
        [DirectiveLocations.NGX_STREAM_MAIN_CONF] = ["stream"],
        [DirectiveLocations.NGX_STREAM_SRV_CONF] = ["stream", "server"],
        [DirectiveLocations.NGX_STREAM_UPS_CONF] = ["stream", "upstream"],
        [DirectiveLocations.NGX_HTTP_MAIN_CONF] = ["http"],
        [DirectiveLocations.NGX_HTTP_SRV_CONF] = ["http",  "server"],
        [DirectiveLocations.NGX_HTTP_LOC_CONF] = ["http",  "location"],
        [DirectiveLocations.NGX_HTTP_UPS_CONF] = ["http",  "upstream"],
        [DirectiveLocations.NGX_HTTP_SIF_CONF] = ["http",  "server", "if"],
        [DirectiveLocations.NGX_HTTP_LIF_CONF] = ["http",  "location", "if"],
        [DirectiveLocations.NGX_HTTP_LMT_CONF] = ["http",  "location", "limit_except"],
    };
}