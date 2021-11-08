using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Vaetech.Runtime.Html.Compressor
{
    /// Original: https://code.google.com/p/htmlcompressor/ 
    public sealed class HtmlCompressor : ICompressor
    {
        /// <summary>
        /// Predefined pattern that matches [<b>&lt;?php ... ?></b>] tags.<br/>
        /// Could be passed inside a list to {@link #setPreservePatterns(List) setPreservePatterns} method.
        /// </summary>	            
        public static readonly Regex PHP_TAG_PATTERN =
            new Regex("<\\?php.*?\\?>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        /// <summary>
        /// Predefined pattern that matches [<b>&lt;% ... %></b>] tags. <br/>
        /// Could be passed inside a list to {@link #setPreservePatterns(List) setPreservePatterns} method.
        /// </summary>
        public static readonly Regex SERVER_SCRIPT_TAG_PATTERN =
            new Regex("<%.*?%>", RegexOptions.Singleline);

        /// <summary>
        /// Predefined pattern that matches [<b>&lt;--# ... --></b>] tags.<br/>
        /// Could be passed inside a list to {@link #setPreservePatterns(List) setPreservePatterns} method.
        /// </summary>
        public static readonly Regex SERVER_SIDE_INCLUDE_PATTERN =
            new Regex("<!--\\s*#.*?-->", RegexOptions.Singleline);

        /// <summary>
        /// Predefined list of tags that are very likely to be block-level. <br/>
        /// Could be passed to {@link #setRemoveSurroundingSpaces(string) setRemoveSurroundingSpaces} method.
        /// </summary>
        public static readonly string BLOCK_TAGS_MIN = "html,head,body,br,p";

        /// <summary>
        /// Predefined list of tags that are block-level by default, excluding [<b>&lt;div></b>] and [<b>&lt;li></b>] tags.<br/> 
        /// Table tags are also included.<br/>
        /// Could be passed to {@link #setRemoveSurroundingSpaces(string) setRemoveSurroundingSpaces} method.
        /// </summary>

        public static readonly string BLOCK_TAGS_MAX = BLOCK_TAGS_MIN +
                                                       ",h1,h2,h3,h4,h5,h6,blockquote,center,dl,fieldset,form,frame,frameset,hr,noframes,ol,table,tbody,tr,td,th,tfoot,thead,ul";

        /// <summary>
        /// Could be passed to {@link #setRemoveSurroundingSpaces(string) setRemoveSurroundingSpaces} method <br/>
        /// to remove all surrounding spaces (not recommended).
        /// </summary>
        public static readonly string ALL_TAGS = "all";

        private bool enabled = true;

        ///javascript and css compressor implementations
        private ICompressor javaScriptCompressor = null;
        private ICompressor cssCompressor = null;

        private List<Regex> preservePatterns = null;

        ///statistics
        private bool generateStatistics = false;
        private HtmlCompressorStatistics statistics = null;

        ///temp replacements for preserved blocks 
        private static readonly string tempCondCommentBlock = "%%%~COMPRESS~COND~{0}~%%%";
        private static readonly string tempPreBlock = "%%%~COMPRESS~PRE~{0}~%%%";
        private static readonly string tempTextAreaBlock = "%%%~COMPRESS~TEXTAREA~{0}~%%%";
        private static readonly string tempScriptBlock = "%%%~COMPRESS~SCRIPT~{0}~%%%";
        private static readonly string tempStyleBlock = "%%%~COMPRESS~STYLE~{0}~%%%";
        private static readonly string tempEventBlock = "%%%~COMPRESS~EVENT~{0}~%%%";
        private static readonly string tempLineBreakBlock = "%%%~COMPRESS~LT~{0}~%%%";
        private static readonly string tempSkipBlock = "%%%~COMPRESS~SKIP~{0}~%%%";
        private static readonly string tempUserBlock = "%%%~COMPRESS~USER{0}~{1}~%%%";

        ///compiled regex patterns
        private static readonly Regex emptyPattern = new Regex("\\s");

        private static readonly Regex skipPattern =
            new Regex("<!--\\s*\\{\\{\\{\\s*-->(.*?)<!--\\s*\\}\\}\\}\\s*-->", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex condCommentPattern =
            new Regex("(<!(?:--)?\\[[^\\]]+?]>)(.*?)(<!\\[[^\\]]+]-->)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex commentPattern =
            new Regex("<!---->|<!--[^\\[].*?-->", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex intertagPattern_TagTag =
            new Regex(">\\s+<", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex intertagPattern_TagCustom =
            new Regex(">\\s+%%%~", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex intertagPattern_CustomTag =
            new Regex("~%%%\\s+<", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex intertagPattern_CustomCustom =
            new Regex("~%%%\\s+%%%~", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex multispacePattern =
            new Regex("\\s+", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex tagEndSpacePattern =
            new Regex("(<(?:[^>]+?))(?:\\s+?)(/?>)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex tagLastUnquotedValuePattern =
            new Regex("=\\s*[a-z0-9-_]+$", RegexOptions.IgnoreCase);

        private static readonly Regex tagQuotePattern =
            new Regex("\\s*=\\s*([\"'])([a-z0-9-_]+?)\\1(/?)(?=[^<]*?>)", RegexOptions.IgnoreCase);

        private static readonly Regex prePattern =
            new Regex("(<pre[^>]*?>)(.*?)(</pre>)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex taPattern =
            new Regex("(<textarea[^>]*?>)(.*?)(</textarea>)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex scriptPattern =
            new Regex("(<script[^>]*?>)(.*?)(</script>)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex stylePattern =
            new Regex("(<style[^>]*?>)(.*?)(</style>)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex tagPropertyPattern =
            new Regex("(\\s\\w+)\\s*=\\s*(?=[^<]*?>)", RegexOptions.IgnoreCase);

        private static readonly Regex cdataPattern =
            new Regex("\\s*<!\\[CDATA\\[(.*?)\\]\\]>\\s*", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex scriptCdataPattern =
            new Regex("/\\*\\s*<!\\[CDATA\\[\\/// </summary>(.*?)/\\*\\]\\]>\\s*\\/// </summary>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex doctypePattern =
            new Regex("<!DOCTYPE[^>]*>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex typeAttrPattern =
            new Regex("type\\s*=\\s*([\\\"']*)(.+?)\\1", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex jsTypeAttrPattern =
            new Regex("(<script[^>]*)type\\s*=\\s*([\"']*)(?:text|application)/javascript\\2([^>]*>)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex jsLangAttrPattern =
            new Regex("(<script[^>]*)language\\s*=\\s*([\"']*)javascript\\2([^>]*>)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex styleTypeAttrPattern =
            new Regex("(<style[^>]*)type\\s*=\\s*([\"']*)text/style\\2([^>]*>)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex linkTypeAttrPattern =
            new Regex("(<link[^>]*)type\\s*=\\s*([\"']*)text/(?:css|plain)\\2([^>]*>)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex linkRelAttrPattern =
            new Regex("<link(?:[^>]*)rel\\s*=\\s*([\"']*)(?:alternate\\s+)?stylesheet\\1(?:[^>]*)>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex formMethodAttrPattern =
            new Regex("(<form[^>]*)method\\s*=\\s*([\"']*)get\\2([^>]*>)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex inputTypeAttrPattern =
            new Regex("(<input[^>]*)type\\s*=\\s*([\"']*)text\\2([^>]*>)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex booleanAttrPattern =
            new Regex("(<\\w+[^>]*)(checked|selected|disabled|readonly)\\s*=\\s*([\"']*)\\w*\\3([^>]*>)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex eventJsProtocolPattern =
            new Regex("^javascript:\\s*(.+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex httpProtocolPattern =
            new Regex("(<[^>]+?(?:href|src|cite|action)\\s*=\\s*['\"])http:(//[^>]+?>)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex httpsProtocolPattern =
            new Regex("(<[^>]+?(?:href|src|cite|action)\\s*=\\s*['\"])https:(//[^>]+?>)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex relExternalPattern =
            new Regex("<(?:[^>]*)rel\\s*=\\s*([\"']*)(?:alternate\\s+)?external\\1(?:[^>]*)>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex eventPattern1 =
            new Regex("(\\son[a-z]+\\s*=\\s*\")([^\"\\\\\\r\\n]*(?:\\\\.[^\"\\\\\\r\\n]*)*)(\")", RegexOptions.IgnoreCase);
        //unmasked: \son[a-z]+\s*=\s*"[^"\\\r\n]*(?:\\.[^"\\\r\n]*)*"

        private static readonly Regex eventPattern2 =
            new Regex("(\\son[a-z]+\\s*=\\s*')([^'\\\\\\r\\n]*(?:\\\\.[^'\\\\\\r\\n]*)*)(')", RegexOptions.IgnoreCase);

        private static readonly Regex lineBreakPattern = new Regex("(?:[ \t]*(\\r?\\n)[ \t]*)+");

        private static readonly Regex surroundingSpacesMinPattern =
            new Regex("\\s*(</?(?:" + BLOCK_TAGS_MIN.Replace(",", "|") + ")(?:>|[\\s/][^>]*>))\\s*", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex surroundingSpacesMaxPattern =
            new Regex("\\s*(</?(?:" + BLOCK_TAGS_MAX.Replace(",", "|") + ")(?:>|[\\s/][^>]*>))\\s*", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex surroundingSpacesAllPattern = new Regex("\\s*(<[^>]+>)\\s*", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        ///patterns for searching for temporary replacements
        private static readonly Regex tempCondCommentPattern = new Regex("%%%~COMPRESS~COND~(\\d+?)~%%%");
        private static readonly Regex tempPrePattern = new Regex("%%%~COMPRESS~PRE~(\\d+?)~%%%");
        private static readonly Regex tempTextAreaPattern = new Regex("%%%~COMPRESS~TEXTAREA~(\\d+?)~%%%");
        private static readonly Regex tempScriptPattern = new Regex("%%%~COMPRESS~SCRIPT~(\\d+?)~%%%");
        private static readonly Regex tempStylePattern = new Regex("%%%~COMPRESS~STYLE~(\\d+?)~%%%");
        private static readonly Regex tempEventPattern = new Regex("%%%~COMPRESS~EVENT~(\\d+?)~%%%");
        private static readonly Regex tempSkipPattern = new Regex("%%%~COMPRESS~SKIP~(\\d+?)~%%%");
        private static readonly Regex tempLineBreakPattern = new Regex("%%%~COMPRESS~LT~(\\d+?)~%%%");

        /// <summary>
        /// The main method that compresses given HTML source and returns compressed result.
        /// </summary>
        /// <param name="html">HTML content to compress</param>
        /// <returns>compressed content.</returns>
        public string compress(string html)
        {
            if (!enabled || string.IsNullOrEmpty(html))
            {
                return html;
            }

            ///calculate uncompressed statistics
            initStatistics(html);

            ///preserved block containers
            List<string> condCommentBlocks = new List<string>();
            List<string> preBlocks = new List<string>();
            List<string> taBlocks = new List<string>();
            List<string> scriptBlocks = new List<string>();
            List<string> styleBlocks = new List<string>();
            List<string> eventBlocks = new List<string>();
            List<string> skipBlocks = new List<string>();
            List<string> lineBreakBlocks = new List<string>();
            List<List<string>> userBlocks = new List<List<string>>();

            ///preserve blocks
            html = preserveBlocks(html, preBlocks, taBlocks, scriptBlocks, styleBlocks, eventBlocks, condCommentBlocks,
                                  skipBlocks, lineBreakBlocks, userBlocks);

            ///process pure html
            html = processHtml(html);

            ///process preserved blocks
            processPreservedBlocks(preBlocks, taBlocks, scriptBlocks, styleBlocks, eventBlocks, condCommentBlocks, skipBlocks,
                                   lineBreakBlocks, userBlocks);

            ///put preserved blocks back
            html = returnBlocks(html, preBlocks, taBlocks, scriptBlocks, styleBlocks, eventBlocks, condCommentBlocks, skipBlocks,
                                lineBreakBlocks, userBlocks);

            ///calculate compressed statistics
            endStatistics(html);

            return html;
        }

        private void initStatistics(string html)
        {
            ///create stats
            if (generateStatistics)
            {
                statistics = new HtmlCompressorStatistics();
                statistics.setTime(DateTime.Now.Ticks);
                statistics.getOriginalMetrics().setFilesize(html.Length);

                ///calculate number of empty chars
                var matcher = emptyPattern.Matches(html);
                foreach (Match match in matcher)
                {
                    statistics.getOriginalMetrics().setEmptyChars(statistics.getOriginalMetrics().getEmptyChars() + 1);
                }
            }
            else
            {
                statistics = null;
            }
        }

        private void endStatistics(string html)
        {
            ///calculate compression time
            if (generateStatistics)
            {
                statistics.setTime(DateTime.Now.Ticks - statistics.getTime());
                statistics.getCompressedMetrics().setFilesize(html.Length);

                ///calculate number of empty chars
                var matcher = emptyPattern.Matches(html);
                foreach (Match match in matcher)
                {
                    statistics.getCompressedMetrics().setEmptyChars(statistics.getCompressedMetrics().getEmptyChars() + 1);
                }
            }
        }

        private string preserveBlocks(
            string html,
            List<string> preBlocks,
            List<string> taBlocks,
            List<string> scriptBlocks,
            List<string> styleBlocks,
            List<string> eventBlocks,
            List<string> condCommentBlocks,
            List<string> skipBlocks, List<string> lineBreakBlocks,
            List<List<string>> userBlocks)
        {
            ///preserve user blocks
            if (preservePatterns != null)
            {
                for (var p = 0; p < preservePatterns.Count; p++)
                {
                    var userBlock = new List<string>();

                    var matches = preservePatterns[p].Matches(html);
                    var index = 0;
                    var sb = new StringBuilder();
                    var lastValue = 0;

                    foreach (Match match in matches)
                    {
                        if (match.Groups[0].Value.Trim().Length > 0)
                        {
                            userBlock.Add(match.Groups[0].Value);

                            sb.Append(html.Substring(lastValue, match.Index - lastValue));
                            sb.Append(match.Result(string.Format(tempUserBlock, p, index++)));

                            lastValue = match.Index + match.Length;
                        }
                    }
                    sb.Append(html.Substring(lastValue));

                    html = sb.ToString();
                    userBlocks.Add(userBlock);
                }
            }

            var skipBlockIndex = 0;

            #region preserve <!-- {{{ ---><!-- }}} ---> skip blocks
            {
                var matcher = skipPattern.Matches(html);
                var sb = new StringBuilder();
                var lastValue = 0;

                foreach (Match match in matcher)
                {
                    if (match.Groups[1].Value.Trim().Length > 0)
                    {
                        skipBlocks.Add(match.Groups[1].Value);

                        sb.Append(html.Substring(lastValue, match.Index - lastValue));
                        sb.Append(match.Result(string.Format(tempSkipBlock, skipBlockIndex++)));

                        lastValue = match.Index + match.Length;
                    }
                }

                sb.Append(html.Substring(lastValue));

                html = sb.ToString();
            }
            #endregion

            #region preserve conditional comments            
            {
                var condCommentCompressor = createCompressorClone();
                var matcher = condCommentPattern.Matches(html);
                var index = 0;
                var sb = new StringBuilder();
                var lastValue = 0;

                foreach (Match match in matcher)
                {
                    if (match.Groups[2].Value.Trim().Length > 0)
                    {
                        condCommentBlocks.Add(
                            match.Groups[1].Value + condCommentCompressor.compress(match.Groups[2].Value) + match.Groups[3].Value);

                        sb.Append(html.Substring(lastValue, match.Index - lastValue));
                        sb.Append(match.Result(string.Format(tempCondCommentBlock, index++)));

                        lastValue = match.Index + match.Length;
                    }
                }

                sb.Append(html.Substring(lastValue));

                html = sb.ToString();
            }
            #endregion

            #region Preserve inline events            
            {
                var matcher = eventPattern1.Matches(html);
                var index = 0;
                var sb = new StringBuilder();
                var lastValue = 0;

                foreach (Match match in matcher)
                {
                    if (match.Groups[2].Value.Trim().Length > 0)
                    {
                        eventBlocks.Add(match.Groups[2].Value);

                        sb.Append(html.Substring(lastValue, match.Index - lastValue));
                        sb.Append(match.Result("$1" + string.Format(tempEventBlock, index++) + "$3"));

                        lastValue = match.Index + match.Length;
                    }
                }

                sb.Append(html.Substring(lastValue));
                html = sb.ToString();
            }
            #endregion

            #region Event Pattern
            {
                var matcher = eventPattern2.Matches(html);
                var index = 0;
                var sb = new StringBuilder();
                var lastValue = 0;

                foreach (Match match in matcher)
                {
                    if (match.Groups[2].Value.Trim().Length > 0)
                    {
                        eventBlocks.Add(match.Groups[2].Value);

                        sb.Append(html.Substring(lastValue, match.Index - lastValue));
                        sb.Append(match.Result("$1" + string.Format(tempEventBlock, index++) + "$3"));

                        lastValue = match.Index + match.Length;
                    }
                }

                sb.Append(html.Substring(lastValue));
                html = sb.ToString();
            }
            #endregion

            #region Preserve PRE tags        
            {
                var matcher = prePattern.Matches(html);
                var index = 0;
                var sb = new StringBuilder();
                var lastValue = 0;

                foreach (Match match in matcher)
                {
                    if (match.Groups[2].Value.Trim().Length > 0)
                    {
                        preBlocks.Add(match.Groups[2].Value);

                        sb.Append(html.Substring(lastValue, match.Index - lastValue));
                        sb.Append(match.Result("$1" + string.Format(tempPreBlock, index++) + "$3"));

                        lastValue = match.Index + match.Length;
                    }
                }

                sb.Append(html.Substring(lastValue));
                html = sb.ToString();
            }
            #endregion

            #region Preserve SCRIPT tags
            {
                var matcher = scriptPattern.Matches(html);
                var index = 0;
                var sb = new StringBuilder();
                var lastValue = 0;

                foreach (Match match in matcher)
                {
                    ///ignore empty scripts
                    if (match.Groups[2].Value.Trim().Length > 0)
                    {

                        ///check type
                        string type = "";
                        var typeMatcher = typeAttrPattern.Match(match.Groups[1].Value);
                        if (typeMatcher.Success)
                        {
                            type = typeMatcher.Groups[2].Value.ToLowerInvariant();
                        }

                        if (type.Length == 0 || type.Equals("text/javascript") || type.Equals("application/javascript"))
                        {
                            ///javascript block, preserve and compress with js compressor
                            scriptBlocks.Add(match.Groups[2].Value);

                            sb.Append(html.Substring(lastValue, match.Index - lastValue));
                            sb.Append(match.Result("$1" + string.Format(tempScriptBlock, index++) + "$3"));

                            lastValue = match.Index + match.Length;
                        }
                        else if (type.Equals("text/x-jquery-tmpl"))
                        {
                            ///jquery template, ignore so it gets compressed with the rest of html
                        }
                        else
                        {
                            ///some custom script, preserve it inside "skip blocks" so it won't be compressed with js compressor 
                            skipBlocks.Add(match.Groups[2].Value);

                            sb.Append(html.Substring(lastValue, match.Index - lastValue));
                            sb.Append(match.Result("$1" + string.Format(tempSkipBlock, skipBlockIndex++) + "$3"));

                            lastValue = match.Index + match.Length;
                        }
                    }
                }

                sb.Append(html.Substring(lastValue));
                html = sb.ToString();
            }
            #endregion

            #region Preserve STYLE tags            
            {
                var matcher = stylePattern.Matches(html);
                var index = 0;
                var sb = new StringBuilder();
                var lastValue = 0;

                foreach (Match match in matcher)
                {
                    if (match.Groups[2].Value.Trim().Length > 0)
                    {
                        styleBlocks.Add(match.Groups[2].Value);

                        sb.Append(html.Substring(lastValue, match.Index - lastValue));
                        sb.Append(match.Result("$1" + string.Format(tempStyleBlock, index++) + "$3"));

                        lastValue = match.Index + match.Length;
                    }
                }

                sb.Append(html.Substring(lastValue));
                html = sb.ToString();
            }
            #endregion

            #region Preserve TEXTAREA tags    
            {
                var matcher = taPattern.Matches(html);
                var index = 0;
                var sb = new StringBuilder();
                var lastValue = 0;

                foreach (Match match in matcher)
                {
                    if (match.Groups[2].Value.Trim().Length > 0)
                    {
                        taBlocks.Add(match.Groups[2].Value);

                        sb.Append(html.Substring(lastValue, match.Index - lastValue));
                        sb.Append(match.Result("$1" + string.Format(tempTextAreaBlock, index++) + "$3"));

                        lastValue = match.Index + match.Length;
                    }
                }

                sb.Append(html.Substring(lastValue));
                html = sb.ToString();
            }
            #endregion

            #region Preserve line breaks
            if (Settings.PreserveLineBreaks)
            {
                var matcher = lineBreakPattern.Matches(html);
                var index = 0;
                var sb = new StringBuilder();
                var lastValue = 0;

                foreach (Match match in matcher)
                {
                    lineBreakBlocks.Add(match.Groups[1].Value);

                    sb.Append(html.Substring(lastValue, match.Index - lastValue));
                    sb.Append(match.Result(string.Format(tempLineBreakBlock, index++)));

                    lastValue = match.Index + match.Length;
                }

                sb.Append(html.Substring(lastValue));
                html = sb.ToString();
            }
            #endregion

            return html;
        }

        private string returnBlocks(
            string html,
            List<string> preBlocks,
            List<string> taBlocks,
            List<string> scriptBlocks,
            List<string> styleBlocks,
            List<string> eventBlocks,
            List<string> condCommentBlocks,
            List<string> skipBlocks,
            List<string> lineBreakBlocks,
            List<List<string>> userBlocks)
        {
            #region Line breaks back
            if (Settings.PreserveLineBreaks)
            {
                var matcher = tempLineBreakPattern.Matches(html);
                var sb = new StringBuilder();
                var lastValue = 0;

                foreach (Match match in matcher)
                {
                    var i = int.Parse(match.Groups[1].Value);
                    if (lineBreakBlocks.Count > i)
                    {
                        sb.Append(html.Substring(lastValue, match.Index - lastValue));
                        sb.Append(lineBreakBlocks[i]);

                        lastValue = match.Index + match.Length;
                    }
                }

                sb.Append(html.Substring(lastValue));
                html = sb.ToString();
            }
            #endregion

            #region TEXTAREA blocks back
            {
                var matcher = tempTextAreaPattern.Matches(html);
                var sb = new StringBuilder();
                var lastValue = 0;

                foreach (Match match in matcher)
                {
                    int i = int.Parse(match.Groups[1].Value);
                    if (taBlocks.Count > i)
                    {
                        sb.Append(html.Substring(lastValue, match.Index - lastValue));
                        sb.Append(taBlocks[i]);

                        lastValue = match.Index + match.Length;
                    }
                }

                sb.Append(html.Substring(lastValue));
                html = sb.ToString();
            }
            #endregion

            #region STYLE blocks back            
            {
                var matcher = tempStylePattern.Matches(html);
                var sb = new StringBuilder();
                var lastValue = 0;

                foreach (Match match in matcher)
                {
                    int i = int.Parse(match.Groups[1].Value);
                    if (styleBlocks.Count > i)
                    {
                        sb.Append(html.Substring(lastValue, match.Index - lastValue));
                        sb.Append(styleBlocks[i]);

                        lastValue = match.Index + match.Length;
                    }
                }

                sb.Append(html.Substring(lastValue));
                html = sb.ToString();
            }
            #endregion

            #region SCRIPT blocks back         
            {
                var matcher = tempScriptPattern.Matches(html);
                var sb = new StringBuilder();
                var lastValue = 0;

                foreach (Match match in matcher)
                {
                    int i = int.Parse(match.Groups[1].Value);
                    if (scriptBlocks.Count > i)
                    {
                        sb.Append(html.Substring(lastValue, match.Index - lastValue));
                        sb.Append(scriptBlocks[i]);

                        lastValue = match.Index + match.Length;
                    }
                }

                sb.Append(html.Substring(lastValue));
                html = sb.ToString();
            }
            #endregion

            #region PRE blocks back            
            {
                var matcher = tempPrePattern.Matches(html);
                var sb = new StringBuilder();
                var lastValue = 0;

                foreach (Match match in matcher)
                {
                    int i = int.Parse(match.Groups[1].Value);
                    if (preBlocks.Count > i)
                    {
                        sb.Append(html.Substring(lastValue, match.Index - lastValue));
                        sb.Append(preBlocks[i]);

                        lastValue = match.Index + match.Length;
                    }
                }

                sb.Append(html.Substring(lastValue));
                html = sb.ToString();
            }
            #endregion

            #region Event blocks back
            {
                var matcher = tempEventPattern.Matches(html);
                var sb = new StringBuilder();
                var lastValue = 0;

                foreach (Match match in matcher)
                {
                    int i = int.Parse(match.Groups[1].Value);
                    if (eventBlocks.Count > i)
                    {
                        sb.Append(html.Substring(lastValue, match.Index - lastValue));
                        sb.Append(eventBlocks[i]);

                        lastValue = match.Index + match.Length;
                    }
                }

                sb.Append(html.Substring(lastValue));
                html = sb.ToString();
            }
            #endregion

            #region Conditional comments back
            {
                var matcher = tempCondCommentPattern.Matches(html);
                var sb = new StringBuilder();
                var lastValue = 0;

                foreach (Match match in matcher)
                {
                    int i = int.Parse(match.Groups[1].Value);
                    if (condCommentBlocks.Count > i)
                    {
                        sb.Append(html.Substring(lastValue, match.Index - lastValue));
                        sb.Append(condCommentBlocks[i]);

                        lastValue = match.Index + match.Length;
                    }
                }

                sb.Append(html.Substring(lastValue));
                html = sb.ToString();
            }
            #endregion

            #region Skip blocks back            
            {
                var matcher = tempSkipPattern.Matches(html);
                var sb = new StringBuilder();
                var lastValue = 0;

                foreach (Match match in matcher)
                {
                    int i = int.Parse(match.Groups[1].Value);
                    if (skipBlocks.Count > i)
                    {
                        sb.Append(html.Substring(lastValue, match.Index - lastValue));
                        sb.Append(skipBlocks[i]);

                        lastValue = match.Index + match.Length;
                    }
                }

                sb.Append(html.Substring(lastValue));
                html = sb.ToString();
            }
            #endregion

            #region User blocks back
            if (preservePatterns != null)
            {
                for (int p = preservePatterns.Count - 1; p >= 0; p--)
                {
                    Regex tempUserPattern = new Regex("%%%~COMPRESS~USER" + p + "~(\\d+?)~%%%");
                    var matcher = tempUserPattern.Matches(html);
                    var sb = new StringBuilder();
                    var lastValue = 0;

                    foreach (Match match in matcher)
                    {
                        int i = int.Parse(match.Groups[1].Value);
                        if (userBlocks.Count > p && userBlocks[p].Count > i)
                        {
                            sb.Append(html.Substring(lastValue, match.Index - lastValue));
                            sb.Append(userBlocks[p][i]);

                            lastValue = match.Index + match.Length;
                        }
                    }

                    sb.Append(html.Substring(lastValue));
                    html = sb.ToString();
                }
            }
            #endregion

            return html;
        }

        private string processHtml(string html)
        {

            ///remove comments
            html = removeComments(html);

            ///simplify doctype
            html = simpleDoctype(html);

            ///remove script attributes
            html = removeScriptAttributes(html);

            ///remove style attributes
            html = removeStyleAttributes(html);

            ///remove link attributes
            html = removeLinkAttributes(html);

            ///remove form attributes
            html = removeFormAttributes(html);

            ///remove input attributes
            html = removeInputAttributes(html);

            ///simplify bool attributes
            html = simpleBooleanAttributes(html);

            ///remove http from attributes
            html = removeHttpProtocol(html);

            ///remove https from attributes
            html = removeHttpsProtocol(html);

            ///remove inter-tag spaces
            html = removeIntertagSpaces(html);

            ///remove multi whitespace characters
            html = removeMultiSpaces(html);

            ///remove spaces around equals sign and ending spaces
            html = removeSpacesInsideTags(html);

            ///remove quotes from tag attributes
            html = removeQuotesInsideTags(html);

            ///remove surrounding spaces
            html = removeSurroundingSpaces(html);

            return html.Trim();
        }

        private string removeSurroundingSpaces(string html)
        {
            ///remove spaces around provided tags
            if (Settings.RemoveSurroundingSpaces != null)
            {
                Regex pattern;
                if (string.Compare(Settings.RemoveSurroundingSpaces, BLOCK_TAGS_MIN, StringComparison.CurrentCultureIgnoreCase) == 0)
                    pattern = surroundingSpacesMinPattern;
                else if (string.Compare(Settings.RemoveSurroundingSpaces, BLOCK_TAGS_MAX, StringComparison.CurrentCultureIgnoreCase) == 0)
                    pattern = surroundingSpacesMaxPattern;
                if (string.Compare(Settings.RemoveSurroundingSpaces, ALL_TAGS, StringComparison.CurrentCultureIgnoreCase) == 0)
                    pattern = surroundingSpacesAllPattern;
                else
                {
                    pattern = new Regex(string.Format("\\s*(</?(?:{0})(?:>|[\\s/][^>]*>))\\s*", Settings.RemoveSurroundingSpaces.Replace(",", "|")), RegexOptions.Singleline | RegexOptions.IgnoreCase);
                }

                var matcher = pattern.Matches(html);
                var sb = new StringBuilder();
                var lastValue = 0;

                foreach (Match match in matcher)
                {
                    sb.Append(html.Substring(lastValue, match.Index - lastValue));
                    //matcher.appendReplacement(sb, "$1");
                    sb.Append(match.Result("$1"));

                    lastValue = match.Index + match.Length;
                }

                //matcher.appendTail(sb);
                sb.Append(html.Substring(lastValue));

                html = sb.ToString();

            }
            return html;
        }

        private string removeQuotesInsideTags(string html)
        {
            ///remove quotes from tag attributes
            if (Settings.RemoveQuotes)
            {
                var matcher = tagQuotePattern.Matches(html);
                var sb = new StringBuilder();
                var lastValue = 0;

                foreach (Match match in matcher)
                {
                    ///if quoted attribute is followed by "/" add extra space
                    if (match.Groups[3].Value.Trim().Length == 0)
                    {
                        sb.Append(html.Substring(lastValue, match.Index - lastValue));
                        sb.Append(match.Result("=$2"));

                        lastValue = match.Index + match.Length;
                    }
                    else
                    {
                        sb.Append(html.Substring(lastValue, match.Index - lastValue));
                        sb.Append(match.Result("=$2 $3"));

                        lastValue = match.Index + match.Length;
                    }
                }

                sb.Append(html.Substring(lastValue));
                html = sb.ToString();
            }
            return html;
        }

        private string removeSpacesInsideTags(string html)
        {
            ///remove spaces around equals sign inside tags
            html = tagPropertyPattern.Replace(html, "$1=");

            ///remove ending spaces inside tags            
            var matcher = tagEndSpacePattern.Matches(html);
            var sb = new StringBuilder();
            var lastValue = 0;

            foreach (Match match in matcher)
            {
                ///keep space if attribute value is unquoted before trailing slash
                if (match.Groups[2].Value.StartsWith("/") && tagLastUnquotedValuePattern.IsMatch(match.Groups[1].Value))
                {
                    sb.Append(html.Substring(lastValue, match.Index - lastValue));
                    sb.Append(match.Result("$1 $2"));

                    lastValue = match.Index + match.Length;
                }
                else
                {
                    sb.Append(html.Substring(lastValue, match.Index - lastValue));
                    sb.Append(match.Result("$1$2"));

                    lastValue = match.Index + match.Length;
                }
            }

            sb.Append(html.Substring(lastValue));
            html = sb.ToString();

            return html;
        }

        private string removeMultiSpaces(string html)
        {
            ///collapse multiple spaces
            if (Settings.RemoveMultiSpaces)
            {
                html = multispacePattern.Replace(html, " ");
            }
            return html;
        }

        private string removeIntertagSpaces(string html)
        {
            ///remove inter-tag spaces
            if (Settings.RemoveIntertagSpaces)
            {
                html = intertagPattern_TagTag.Replace(html, "><");
                html = intertagPattern_TagCustom.Replace(html, ">%%%~");
                html = intertagPattern_CustomTag.Replace(html, "~%%%<");
                html = intertagPattern_CustomCustom.Replace(html, "~%%%%%%~");
            }
            return html;
        }

        private string removeComments(string html)
        {
            ///remove comments
            if (Settings.RemoveComments)
            {
                html = commentPattern.Replace(html, "");
            }
            return html;
        }

        private string simpleDoctype(string html)
        {
            ///simplify doctype
            if (Settings.SimpleDoctype)
            {
                html = doctypePattern.Replace(html, "<!DOCTYPE html>");
            }
            return html;
        }

        private string removeScriptAttributes(string html)
        {
            if (Settings.RemoveScriptAttributes)
            {
                ///remove type from script tags
                html = jsTypeAttrPattern.Replace(html, "$1$3");

                ///remove language from script tags
                html = jsLangAttrPattern.Replace(html, "$1$3");
            }
            return html;
        }

        private string removeStyleAttributes(string html)
        {
            ///remove type from style tags
            if (Settings.RemoveStyleAttributes)
            {
                html = styleTypeAttrPattern.Replace(html, "$1$3");
            }
            return html;
        }

        private string removeLinkAttributes(string html)
        {
            ///remove type from link tags with rel=stylesheet
            if (Settings.RemoveLinkAttributes)
            {
                var matcher = linkTypeAttrPattern.Matches(html);
                var sb = new StringBuilder();
                var lastValue = 0;

                foreach (Match match in matcher)
                {
                    ///if rel=stylesheet
                    if (matches(linkRelAttrPattern, match.Groups[0].Value))
                    {
                        sb.Append(html.Substring(lastValue, match.Index - lastValue));
                        sb.Append(match.Result("$1$3"));

                        lastValue = match.Index + match.Length;
                    }
                    else
                    {
                        sb.Append(html.Substring(lastValue, match.Index - lastValue));
                        sb.Append(match.Result("$0"));

                        lastValue = match.Index + match.Length;
                    }
                }

                sb.Append(html.Substring(lastValue));
                html = sb.ToString();
            }
            return html;
        }

        private string removeFormAttributes(string html)
        {
            ///remove method from form tags
            if (Settings.RemoveFormAttributes)
            {
                html = formMethodAttrPattern.Replace(html, "$1$3");
            }
            return html;
        }

        private string removeInputAttributes(string html)
        {
            ///remove type from input tags
            if (Settings.RemoveInputAttributes)
            {
                html = inputTypeAttrPattern.Replace(html, "$1$3");
            }
            return html;
        }

        private string simpleBooleanAttributes(string html)
        {
            ///simplify bool attributes
            if (Settings.SimpleBooleanAttributes)
            {
                html = booleanAttrPattern.Replace(html, "$1$2$4");
            }
            return html;
        }

        private string removeHttpProtocol(string html)
        {
            ///remove http protocol from tag attributes
            if (Settings.RemoveHttpProtocol)
            {
                var matcher = httpProtocolPattern.Matches(html);
                var sb = new StringBuilder();
                var lastValue = 0;

                foreach (Match match in matcher)
                {
                    ///if rel!=external
                    if (!matches(relExternalPattern, match.Groups[0].Value))
                    {
                        sb.Append(html.Substring(lastValue, match.Index - lastValue));
                        sb.Append(match.Result("$1$2"));

                        lastValue = match.Index + match.Length;
                    }
                    else
                    {
                        sb.Append(html.Substring(lastValue, match.Index - lastValue));
                        sb.Append(match.Result("$0"));

                        lastValue = match.Index + match.Length;
                    }
                }

                sb.Append(html.Substring(lastValue));
                html = sb.ToString();
            }
            return html;
        }

        private string removeHttpsProtocol(string html)
        {
            ///remove https protocol from tag attributes
            if (Settings.RemoveHttpsProtocol)
            {
                var matcher = httpsProtocolPattern.Matches(html);
                var sb = new StringBuilder();
                var lastValue = 0;

                foreach (Match match in matcher)
                {
                    ///if rel!=external
                    if (!matches(relExternalPattern, match.Groups[0].Value))
                    {
                        sb.Append(html.Substring(lastValue, match.Index - lastValue));
                        sb.Append(match.Result("$1$2"));

                        lastValue = match.Index + match.Length;
                    }
                    else
                    {
                        sb.Append(html.Substring(lastValue, match.Index - lastValue));
                        sb.Append(match.Result("$0"));

                        lastValue = match.Index + match.Length;
                    }
                }

                sb.Append(html.Substring(lastValue));
                html = sb.ToString();
            }
            return html;
        }

        private static bool matches(Regex regex, string value)
        {
            /// http://stackoverflow.com/questions/4450045/difference-between-matches-and-find-in-java-regex
            var cloneRegex = new Regex(@"^" + regex + @"$", regex.Options);
            return cloneRegex.IsMatch(value);
        }

        private void processPreservedBlocks(List<string> preBlocks, List<string> taBlocks, List<string> scriptBlocks,
                                            List<string> styleBlocks, List<string> eventBlocks, List<string> condCommentBlocks,
                                            List<string> skipBlocks, List<string> lineBreakBlocks,
                                            List<List<string>> userBlocks)
        {
            processPreBlocks(preBlocks);
            processTextAreaBlocks(taBlocks);
            processScriptBlocks(scriptBlocks);
            processStyleBlocks(styleBlocks);
            processEventBlocks(eventBlocks);
            processCondCommentBlocks(condCommentBlocks);
            processSkipBlocks(skipBlocks);
            processUserBlocks(userBlocks);
            processLineBreakBlocks(lineBreakBlocks);
        }

        private void processPreBlocks(List<string> preBlocks)
        {
            if (generateStatistics)
            {
                foreach (string block in preBlocks)
                {
                    statistics.setPreservedSize(statistics.getPreservedSize() + block.Length);
                }
            }
        }

        private void processTextAreaBlocks(List<string> taBlocks)
        {
            if (generateStatistics)
            {
                foreach (string block in taBlocks)
                {
                    statistics.setPreservedSize(statistics.getPreservedSize() + block.Length);
                }
            }
        }

        private void processCondCommentBlocks(List<string> condCommentBlocks)
        {
            if (generateStatistics)
            {
                foreach (string block in condCommentBlocks)
                {
                    statistics.setPreservedSize(statistics.getPreservedSize() + block.Length);
                }
            }
        }

        private void processSkipBlocks(List<string> skipBlocks)
        {
            if (generateStatistics)
            {
                foreach (string block in skipBlocks)
                {
                    statistics.setPreservedSize(statistics.getPreservedSize() + block.Length);
                }
            }
        }

        private void processLineBreakBlocks(List<string> lineBreakBlocks)
        {
            if (generateStatistics)
            {
                foreach (string block in lineBreakBlocks)
                {
                    statistics.setPreservedSize(statistics.getPreservedSize() + block.Length);
                }
            }
        }

        private void processUserBlocks(List<List<string>> userBlocks)
        {
            if (generateStatistics)
            {
                foreach (List<string> blockList in userBlocks)
                {
                    foreach (string block in blockList)
                    {
                        statistics.setPreservedSize(statistics.getPreservedSize() + block.Length);
                    }
                }
            }
        }

        private void processEventBlocks(List<string> eventBlocks)
        {

            if (generateStatistics)
            {
                foreach (string block in eventBlocks)
                {
                    statistics.getOriginalMetrics()
                              .setInlineEventSize(statistics.getOriginalMetrics().getInlineEventSize() + block.Length);
                }
            }

            if (Settings.RemoveJavaScriptProtocol)
            {
                for (int i = 0; i < eventBlocks.Count; i++)
                {
                    eventBlocks[i] = removeJavaScriptProtocol(eventBlocks[i]);
                }
            }
            else if (generateStatistics)
            {
                foreach (string block in eventBlocks)
                {
                    statistics.setPreservedSize(statistics.getPreservedSize() + block.Length);
                }
            }

            if (generateStatistics)
            {
                foreach (string block in eventBlocks)
                {
                    statistics.getCompressedMetrics()
                              .setInlineEventSize(statistics.getCompressedMetrics().getInlineEventSize() + block.Length);
                }
            }
        }

        private string removeJavaScriptProtocol(string source)
        {
            ///remove javascript: from inline events            
            string result = eventJsProtocolPattern.Replace(source, @"$1", 1);
            if (generateStatistics)
            {
                statistics.setPreservedSize(statistics.getPreservedSize() + result.Length);
            }

            return result;
        }

        private void processScriptBlocks(List<string> scriptBlocks)
        {

            if (generateStatistics)
            {
                foreach (string block in scriptBlocks)
                {
                    statistics.getOriginalMetrics()
                              .setInlineScriptSize(statistics.getOriginalMetrics().getInlineScriptSize() + block.Length);
                }
            }

            if (Settings.CompressJavaScript)
            {
                for (int i = 0; i < scriptBlocks.Count; i++)
                {
                    scriptBlocks[i] = compressJavaScript(scriptBlocks[i]);
                }
            }
            else if (generateStatistics)
            {
                foreach (string block in scriptBlocks)
                {
                    statistics.setPreservedSize(statistics.getPreservedSize() + block.Length);
                }
            }

            if (generateStatistics)
            {
                foreach (string block in scriptBlocks)
                {
                    statistics.getCompressedMetrics()
                              .setInlineScriptSize(statistics.getCompressedMetrics().getInlineScriptSize() + block.Length);
                }
            }
        }

        private void processStyleBlocks(List<string> styleBlocks)
        {

            if (generateStatistics)
            {
                foreach (string block in styleBlocks)
                {
                    statistics.getOriginalMetrics()
                              .setInlineStyleSize(statistics.getOriginalMetrics().getInlineStyleSize() + block.Length);
                }
            }

            if (Settings.CompressCss)
            {
                for (int i = 0; i < styleBlocks.Count; i++)
                {
                    styleBlocks[i] = compressCssStyles(styleBlocks[i]);
                }
            }
            else if (generateStatistics)
            {
                foreach (string block in styleBlocks)
                {
                    statistics.setPreservedSize(statistics.getPreservedSize() + block.Length);
                }
            }

            if (generateStatistics)
            {
                foreach (string block in styleBlocks)
                {
                    statistics.getCompressedMetrics()
                              .setInlineStyleSize(statistics.getCompressedMetrics().getInlineStyleSize() + block.Length);
                }
            }
        }

        private string compressJavaScript(string source)
        {
            ///set default javascript compressor
            if (javaScriptCompressor == null)
            {
                return source;
            }

            ///detect CDATA wrapper
            bool scriptCdataWrapper = false;
            bool cdataWrapper = false;
            var matcher = scriptCdataPattern.Match(source);
            if (matcher.Success)
            {
                scriptCdataWrapper = true;
                source = matcher.Groups[1].Value;
            }
            else if (cdataPattern.Match(source).Success)
            {
                cdataWrapper = true;
                source = matcher.Groups[1].Value;
            }

            string result = javaScriptCompressor.compress(source);

            if (scriptCdataWrapper)
            {
                result = string.Format("/*<![CDATA[/// </summary>{0}/*]]>/// </summary>", result);
            }
            else if (cdataWrapper)
            {
                result = string.Format("<![CDATA[{0}]]>", result);
            }

            return result;
        }

        private string compressCssStyles(string source)
        {
            ///set default css compressor
            if (cssCompressor == null)
            {
                return source;
            }

            ///detect CDATA wrapper
            bool cdataWrapper = false;
            var matcher = cdataPattern.Match(source);
            if (matcher.Success)
            {
                cdataWrapper = true;
                source = matcher.Groups[1].Value;
            }

            string result = cssCompressor.compress(source);

            if (cdataWrapper)
            {
                result = string.Format("<![CDATA[{0}]]>", result);
            }

            return result;
        }

        private HtmlCompressor createCompressorClone()
        {
            var clone = new HtmlCompressor();
            clone.setJavaScriptCompressor(javaScriptCompressor);
            clone.setCssCompressor(cssCompressor);
            clone.setRemoveComments(Settings.RemoveComments);
            clone.setRemoveMultiSpaces(Settings.RemoveMultiSpaces);
            clone.setRemoveIntertagSpaces(Settings.RemoveIntertagSpaces);
            clone.setRemoveQuotes(Settings.RemoveQuotes);
            clone.setCompressJavaScript(Settings.CompressJavaScript);
            clone.setCompressCss(Settings.CompressCss);
            clone.setSimpleDoctype(Settings.SimpleDoctype);
            clone.setRemoveScriptAttributes(Settings.RemoveScriptAttributes);
            clone.setRemoveStyleAttributes(Settings.RemoveStyleAttributes);
            clone.setRemoveLinkAttributes(Settings.RemoveLinkAttributes);
            clone.setRemoveFormAttributes(Settings.RemoveFormAttributes);
            clone.setRemoveInputAttributes(Settings.RemoveInputAttributes);
            clone.setSimpleBooleanAttributes(Settings.SimpleBooleanAttributes);
            clone.setRemoveJavaScriptProtocol(Settings.RemoveJavaScriptProtocol);
            clone.setRemoveHttpProtocol(Settings.RemoveHttpProtocol);
            clone.setRemoveHttpsProtocol(Settings.RemoveHttpsProtocol);
            clone.setPreservePatterns(preservePatterns);

            return clone;

        }

        /// <summary>Returns <b>True</b> if JavaScript compression is enabled.</summary>         
        public bool isCompressJavaScript() => Settings.CompressJavaScript;

        /// <summary>
        /// Enables JavaScript compression within &lt;script> tags using 
        /// <a href="http://developer.yahoo.com/yui/compressor/">Yahoo YUI ICompressor</a> 
        /// if set to <b>True</b>. Default is <b>False</b> for performance reasons.<br/><br/>
        ///   
        /// <note type="important"><b>Note:</b> Compressing JavaScript is not recommended if pages are 
        /// compressed dynamically on-the-fly because of performance impact. 
        /// You should consider putting JavaScript into a separate file and
        /// compressing it using standalone YUICompressor for example.</note><br/><br/>  
        ///                  
        /// Default is <b>False</b><br/><br/>
        /// <see href="http://developer.yahoo.com/yui/compressor/">Yahoo YUI ICompressor</see><br/>        
        /// </summary>
        /// <param name="compressJavaScript">Set <b>True</b> to enable JavaScript compression. </param>
        public void setCompressJavaScript(bool compressJavaScript) => Settings.CompressJavaScript = compressJavaScript;

        /// <summary>Returns <b>True</b> if CSS compression is enabled.</summary>
        public bool isCompressCss() => Settings.CompressCss;

        /// <summary>
        /// Enables CSS compression within &lt;style> tags using 
        /// <a href="http://developer.yahoo.com/yui/compressor/">Yahoo YUI ICompressor</a> 
        /// if set to <b>True</b>. Default is <b>False</b> for performance reasons.<br/><br/>
        ///  
        /// <note type="important"><b>Note:</b> Compressing CSS is not recommended if pages are 
        /// compressed dynamically on-the-fly because of performance impact. 
        /// You should consider putting CSS into a separate file and
        /// compressing it using standalone YUICompressor for example.</note><br/><br/>
        ///         
        /// Default is <b>False</b>
        /// </summary>
        /// <param name="compressCss">Set <b>True</b> to enable CSS compression. </param>
        public void setCompressCss(bool compressCss) => Settings.CompressCss = compressCss;

        /// <summary> Returns <b>True</b> if all unnecessary quotes will be removed from tag attributes.</summary>         
        public bool isRemoveQuotes() => Settings.RemoveQuotes;

        /// <summary> 
        /// If set to <b>True</b> all unnecessary quotes will be removed  
        /// from tag attributes. Default is <b>False</b>.<br/><br/>
        /// 
        /// <note type="important"><b>Note:</b> Even though quotes are removed only when it is safe to do so, 
        /// it still might break strict HTML validation. Turn this option on only if 
        /// a page validation is not very important or to squeeze the most out of the compression.
        /// This option has no performance impact.</note> <br/><br/>        
        /// </summary>
        /// <param name="removeQuotes">Set <b>True</b> to remove unnecessary quotes from tag attributes</param>
        public void setRemoveQuotes(bool removeQuotes) => Settings.RemoveQuotes = removeQuotes;

        /// <summary> Returns <b>True</b> if compression is enabled. </summary>
        public bool isEnabled() => enabled;

        /// <summary>
        /// If set to <b>False</b> all compression will be bypassed. Might be useful for testing purposes. 
        /// Default is <b>True</b>.<br/><br/>
        /// 
        /// @param enabled set <b>False</b> to bypass all compression
        /// </summary>
        public void setEnabled(bool enabled) => this.enabled = enabled;

        /// <summary> Returns <b>True</b> if all HTML comments will be removed.</summary>      
        public bool isRemoveComments() => Settings.RemoveComments;

        /// <summary>
        /// If set to <b>True</b> all HTML comments will be removed.   
        /// Default is <b>True</b>.<br/><br/>
        /// 
        /// @param removeComments set <b>True</b> to remove all HTML comments
        /// </summary>
        public void setRemoveComments(bool removeComments) => Settings.RemoveComments = removeComments;

        /// <summary> Returns <b>true</b> if all multiple whitespace characters will be replaced with single spaces.</summary>
        public bool isRemoveMultiSpaces() => Settings.RemoveMultiSpaces;

        /// <summary>
        /// If set to <b>True</b> all multiple whitespace characters will be replaced with single spaces.<br/>
        /// Default is <b>True</b>.         
        /// </summary>
        public void setRemoveMultiSpaces(bool removeMultiSpaces) => Settings.RemoveMultiSpaces = removeMultiSpaces;

        /// <summary> Returns <b>True</b> if all inter-tag whitespace characters will be removed.</summary>   
        public bool isRemoveIntertagSpaces() => Settings.RemoveIntertagSpaces;

        /// <summary>
        /// If set to <b>True</b> all inter-tag whitespace characters will be removed.
        /// Default is <b>False</b>.<br/><br/>
        /// 
        /// <p><b>Note:</b> It is fairly safe to turn this option on unless you 
        /// rely on spaces for page formatting. Even if you do, you can always preserve 
        /// required spaces with <b>&amp;nbsp;</b>. This option has no performance impact.</p><br/><br/>
        /// 
        /// @param removeIntertagSpaces set <b>True</b> to remove all inter-tag whitespace characters
        /// </summary>

        public void setRemoveIntertagSpaces(bool removeIntertagSpaces) => Settings.RemoveIntertagSpaces = removeIntertagSpaces;

        /// <summary> Return list of <b>Regex</b> objects defining rules for preserving block rules </summary>       
        public List<Regex> getPreservePatterns() => preservePatterns;

        /// <summary>
        /// This method allows setting custom block preservation rules defined by regular 
        /// expression patterns. Blocks that match provided patterns will be skipped during HTML compression. <br/><br/>
        /// 
        /// <p>Custom preservation rules have higher priority than default rules.
        /// Priority between custom rules are defined by their position in a list 
        /// (beginning of a list has higher priority).</p><br/><br/>
        /// 
        /// <p>Besides custom patterns, you can use 3 predefined patterns: <br/>
        /// <code>
        /// {@link #PHP_TAG_PATTERN PHP_TAG_PATTERN},<br/>
        /// {@link #SERVER_SCRIPT_TAG_PATTERN SERVER_SCRIPT_TAG_PATTERN},<br/>
        /// {@link #SERVER_SIDE_INCLUDE_PATTERN SERVER_SIDE_INCLUDE_PATTERN}.
        /// </code>
        /// </p><br/><br/>
        /// @param preservePatterns List of <b>Regex</b> objects that will be 
        /// used to skip matched blocks during compression  
        /// </summary>
        public void setPreservePatterns(List<Regex> preservePatterns) => this.preservePatterns = preservePatterns;

        /// <summary>
        /// Returns JavaScript compressor implementation that will be used 
        /// to compress inline JavaScript in HTML.<br/><br/>
        /// 
        /// @return <b>ICompressor</b> implementation that will be used 
        /// to compress inline JavaScript in HTML.<br/><br/>
        /// 
        /// @see YuiJavaScriptCompressor<br/>
        /// @see ClosureJavaScriptCompressor<br/>
        /// @see <a href="http://developer.yahoo.com/yui/compressor/">Yahoo YUI ICompressor</a><br/>
        /// @see <a href="http://code.google.com/closure/compiler/">Google Closure Compiler</a><br/>
        /// </summary>
        public ICompressor getJavaScriptCompressor() => javaScriptCompressor;

        /// <summary>
        /// Sets JavaScript compressor implementation that will be used 
        /// to compress inline JavaScript in HTML. 
        /// 
        /// <p>HtmlCompressor currently 
        /// comes with basic implementations for <a href="http://developer.yahoo.com/yui/compressor/">Yahoo YUI ICompressor</a> (called {@link YuiJavaScriptCompressor})
        /// and <a href="http://code.google.com/closure/compiler/">Google Closure Compiler</a> (called {@link ClosureJavaScriptCompressor}) that should be enough for most cases, 
        /// but users can also create their own JavaScript compressors for custom needs.
        /// 
        /// <p>If no compressor is set {@link YuiJavaScriptCompressor} will be used by default.  
        /// 
        /// @param javaScriptCompressor {@link ICompressor} implementation that will be used for inline JavaScript compression
        /// 
        /// @see YuiJavaScriptCompressor
        /// @see ClosureJavaScriptCompressor
        /// @see <a href="http://developer.yahoo.com/yui/compressor/">Yahoo YUI ICompressor</a>
        /// @see <a href="http://code.google.com/closure/compiler/">Google Closure Compiler</a>
        /// </summary>
        public void setJavaScriptCompressor(ICompressor javaScriptCompressor) => this.javaScriptCompressor = javaScriptCompressor;

        /// <summary>
        /// Returns CSS compressor implementation that will be used 
        /// to compress inline CSS in HTML.
        /// 
        /// @return <b>ICompressor</b> implementation that will be used 
        /// to compress inline CSS in HTML.
        /// 
        /// @see YuiCssCompressor
        /// @see <a href="http://developer.yahoo.com/yui/compressor/">Yahoo YUI ICompressor</a>
        /// </summary>

        public ICompressor getCssCompressor() => cssCompressor;

        /// <summary>
        /// Sets CSS compressor implementation that will be used 
        /// to compress inline CSS in HTML. 
        /// 
        /// <p>HtmlCompressor currently 
        /// comes with basic implementation for <a href="http://developer.yahoo.com/yui/compressor/">Yahoo YUI ICompressor</a> (called {@link YuiCssCompressor}), 
        /// but users can also create their own CSS compressors for custom needs. 
        /// 
        /// <p>If no compressor is set {@link YuiCssCompressor} will be used by default.  
        /// 
        /// @param cssCompressor {@link ICompressor} implementation that will be used for inline CSS compression
        /// 
        /// @see YuiCssCompressor
        /// @see <a href="http://developer.yahoo.com/yui/compressor/">Yahoo YUI ICompressor</a>
        /// </summary>

        public void setCssCompressor(ICompressor cssCompressor) => this.cssCompressor = cssCompressor;

        /// <summary>Returns <b>True</b> if existing DOCTYPE declaration will be replaced with simple [<b>&lt;!DOCTYPE html></b>] declaration.</summary>
        public bool isSimpleDoctype() => Settings.SimpleDoctype;

        /// <summary>
        /// If set to <b>True</b>, existing DOCTYPE declaration will be replaced with simple [<b>&lt;!DOCTYPE html></b>] declaration.
        /// Default is <b>False</b>.         
        /// </summary>
        /// <param name="simpleDoctype">set <b>True</b> to replace existing DOCTYPE declaration with [<b>&lt;!DOCTYPE html></b>]</param>

        public void setSimpleDoctype(bool simpleDoctype) => Settings.SimpleDoctype = simpleDoctype;

        /// <summary>Returns <b>True</b> if unnecessary attributes wil be removed from [<b>&lt;script></b>] tags </summary>
        public bool isRemoveScriptAttributes() => Settings.RemoveScriptAttributes;

        /// <summary>
        /// If set to <b>True</b>, following attributes will be removed from <b>&lt;script></b> tags: 
        /// <ul>
        /// <li>type="text/javascript"</li>
        /// <li>type="application/javascript"</li>
        /// <li>language="javascript"</li>
        /// </ul>
        /// 
        /// <p>Default is <b>False</b>.
        /// 
        /// @param removeScriptAttributes set <b>True</b> to remove unnecessary attributes from <b>&lt;script></b> tags 
        /// </summary>

        public void setRemoveScriptAttributes(bool removeScriptAttributes) => Settings.RemoveScriptAttributes = removeScriptAttributes;

        /// <summary>Returns <b>True</b> if <b>type="text/style"</b> attributes will be removed from <b>&lt;style></b> tags</summary>
        public bool isRemoveStyleAttributes() => Settings.RemoveStyleAttributes;

        /// <summary>
        /// If set to <b>True</b>, <b>type="text/style"</b> attributes will be removed from <b>&lt;style></b> tags. Default is <b>False</b>.
        /// 
        /// @param removeStyleAttributes set <b>True</b> to remove <b>type="text/style"</b> attributes from <b>&lt;style></b> tags
        /// </summary>
        public void setRemoveStyleAttributes(bool removeStyleAttributes) => Settings.RemoveStyleAttributes = removeStyleAttributes;

        /// <summary>Returns <b>True</b> if unnecessary attributes will be removed from <b>&lt;link></b> tags</summary>         
        public bool isRemoveLinkAttributes() => Settings.RemoveLinkAttributes;

        /// <summary>
        /// If set to <b>True</b>, following attributes will be removed from <c>&lt;link rel="stylesheet"></c> and <b>&lt;link rel="alternate stylesheet"></b> tags: <br/>        
        /// <c>type="text/css"</c><br/>
        /// <c>type="text/plain"</c><br/><br/>
        /// 
        /// Default is <b>False</b>.<br/>
        /// </summary>
        /// <param name="removeLinkAttributes">set <b>True</b> to remove unnecessary attributes from <b>&lt;link></b> tags</param>

        public void setRemoveLinkAttributes(bool removeLinkAttributes) => Settings.RemoveLinkAttributes = removeLinkAttributes;

        /// <summary>
        /// Returns <b>True</b> if <b>method="get"</b> attributes will be removed from <b>&lt;form></b> tags         
        /// </summary>

        public bool isRemoveFormAttributes() => Settings.RemoveFormAttributes;

        /// <summary>
        /// If set to <b>True</b>, <b>method="get"</b> attributes will be removed from <b>&lt;form></b> tags. Default is <b>False</b>.
        /// 
        /// @param removeFormAttributes set <b>True</b> to remove <b>method="get"</b> attributes from <b>&lt;form></b> tags
        /// </summary>

        public void setRemoveFormAttributes(bool removeFormAttributes) => Settings.RemoveFormAttributes = removeFormAttributes;

        /// <summary> Returns <b>True</b> if <b>type="text"</b> attributes will be removed from <b>&lt;input></b> tags</summary>
        public bool isRemoveInputAttributes() => Settings.RemoveInputAttributes;

        /// <summary> If set to <b>True</b>, <b>type="text"</b> attributes will be removed from <b>&lt;input></b> tags. Default is <b>False</b>.</summary>
        public void setRemoveInputAttributes(bool removeInputAttributes) => Settings.RemoveInputAttributes = removeInputAttributes;

        /// <summary> Returns <b>True</b> if bool attributes will be simplified</summary>
        public bool isSimpleBooleanAttributes() => Settings.SimpleBooleanAttributes;

        /// <summary>
        /// If set to <b>True</b>, any values of following bool attributes will be removed:
        /// <code> <b>checked</b>, <b>selected</b>, <b>disabled</b>, <b>readonly</b></code>
        /// 
        /// For example, <b>[&lt;input readonly="true">]</b> would become <b>[&lt;input readonly>]</b>        
        /// Default is <b>False</b>.<br/><br/>
        ///         
        /// </summary>
        /// <param name="simpleBooleanAttributes">Set <b>True</b> to simplify bool attributes</param>
        public void setSimpleBooleanAttributes(bool simpleBooleanAttributes) => Settings.SimpleBooleanAttributes = simpleBooleanAttributes;

        /// <summary>Returns <b>True</b> if <b>javascript:</b> pseudo-protocol will be removed from inline event handlers.</summary>
        public bool isRemoveJavaScriptProtocol() => Settings.RemoveJavaScriptProtocol;

        /// <summary>
        /// If set to <b>True</b>, [<b>javascript:</b>] pseudo-protocol will be removed from inline event handlers.<br/><br/>
        /// For example, [<b>&lt;a onclick="javascript:alert()"></b>] would become [<b>&lt;a onclick="alert()"></b>]<br/>
        /// Default is <b>False</b>.<br/><br/>      
        /// </summary>
        /// <param name="removeJavaScriptProtocol">Set <b>True</b> to remove <b>javascript:</b> pseudo-protocol from inline event handlers.</param>

        public void setRemoveJavaScriptProtocol(bool removeJavaScriptProtocol) => Settings.RemoveJavaScriptProtocol = removeJavaScriptProtocol;

        /// <summary>Returns <b>True</b> if <b>HTTP</b> protocol will be removed from <b>href</b>, <b>src</b>, <b>cite</b>, and <b>action</b> tag attributes.</summary>

        public bool isRemoveHttpProtocol() => Settings.RemoveHttpProtocol;

        /// <summary>
        /// If set to <b>True</b>, <b>HTTP</b> protocol will be removed from <b>href</b>, <b>src</b>, <b>cite</b>, and <b>action</b> tag attributes.
        /// URL without a protocol would make a browser use document's current protocol instead. <br/><br/>
        /// 
        /// Tags marked with <b>rel="external"</b> will be skipped.<br/><br/>
        /// 
        /// For example: <br/>
        /// <b>&lt;a href="http://example.com"> &lt;script src="http://google.com/js.js" rel="external"></b> <br/>
        /// would become: <br/>
        /// <b>&lt;a href="//example.com"> &lt;script src="http://google.com/js.js" rel="external"></b><br/><br/>
        /// 
        /// Default is <b>False</b>.        
        /// </summary>
        /// <param name="removeHttpProtocol">Set <b>True</b> to remove <b>HTTP</b> protocol from tag attributes</param>

        public void setRemoveHttpProtocol(bool removeHttpProtocol) => Settings.RemoveHttpProtocol = removeHttpProtocol;

        /// <summary>Returns <b>True</b> if <b>HTTPS</b> protocol will be removed from <b>href</b>, <b>src</b>, <b>cite</b>, and <b>action</b> tag attributes.</summary>
        public bool isRemoveHttpsProtocol() => Settings.RemoveHttpsProtocol;

        /// <summary>
        /// If set to <b>True</b>, <b>HTTPS</b> protocol will be removed from <b>href</b>, <b>src</b>, <b>cite</b>, and <b>action</b> tag attributes.
        /// URL without a protocol would make a browser use document's current protocol instead.<br/><br/>
        /// 
        /// Tags marked with <b>rel="external"</b> will be skipped.<br/><br/>
        /// 
        /// For example: <br/>
        /// <b>&lt;a href="https://example.com"> &lt;script src="https://google.com/js.js" rel="external"></b><br/> 
        /// would become: <br/>
        /// <b>&lt;a href="//example.com"> &lt;script src="https://google.com/js.js" rel="external"></b><br/><br/>
        /// 
        /// Default is <b>False</b>.<br/>                
        /// </summary>
        /// <param name="removeHttpsProtocol">Set <b>True</b> to remove <b>HTTP</b> protocol from tag attributes</param>

        public void setRemoveHttpsProtocol(bool removeHttpsProtocol) => Settings.RemoveHttpsProtocol = removeHttpsProtocol;

        /// <summary>Returns <b>True</b> if HTML compression statistics is generated</summary> 
        public bool isGenerateStatistics() => generateStatistics;

        /// <summary>
        /// If set to <b>True</b>, HTML compression statistics will be generated. <br/><br/> 
        /// 
        /// <strong>Important:</strong> Enabling statistics makes HTML compressor not thread safe. <br/><br/> 
        /// 
        /// Default is <b>False</b>.<br/><br/>         
        ///         
        /// <see cref="getStatistics()"/>
        /// </summary>
        /// <param name="generateStatistics">Set <b>True</b> to generate HTML compression statistics </param>

        public void setGenerateStatistics(bool generateStatistics) => this.generateStatistics = generateStatistics;

        /// <summary>
        /// Returns {@link HtmlCompressorStatistics} object containing statistics of the last HTML compression, if enabled. <br/>
        /// Should be called after {@link #compress(string)}<br/><br/>        
        /// 
        /// <see cref="HtmlCompressorStatistics"/><br/>
        /// <see cref="setGenerateStatistics(bool)"/>        
        /// </summary>        
        /// <returns>{@link HtmlCompressorStatistics} object containing last HTML compression statistics<br/><br/></returns>

        public HtmlCompressorStatistics getStatistics() => statistics;

        /// <summary>Returns <b>True</b> if line breaks will be preserved.</summary> 
        public bool isPreserveLineBreaks() => Settings.PreserveLineBreaks;

        /// <summary>
        /// If set to <b>True</b>, line breaks will be preserved. <br/><br/>
        /// Default is <b>False</b>.<br/><br/>        
        /// </summary>
        /// <param name="preserveLineBreaks">Set <b>True</b> to preserve line breaks</param>

        public void setPreserveLineBreaks(bool preserveLineBreaks) => Settings.PreserveLineBreaks = preserveLineBreaks;

        /// <summary>Returns a comma separated list of tags around which spaces will be removed.</summary>
        public string getRemoveSurroundingSpaces() => Settings.RemoveSurroundingSpaces;

        /// <summary>
        /// Enables surrounding spaces removal around provided comma separated list of tags.<br/><br/>
        /// 
        /// Besides custom defined lists, you can pass one of 3 predefined lists of tags: <br/>
        /// <code>
        /// {@link #BLOCK_TAGS_MIN BLOCK_TAGS_MIN},<br/>
        /// {@link #BLOCK_TAGS_MAX BLOCK_TAGS_MAX},<br/>
        /// {@link #ALL_TAGS ALL_TAGS}.<br/>                
        /// </code>
        /// </summary>
        /// <param name="tagList">a comma separated list of tags around which spaces will be removed</param>

        public void setRemoveSurroundingSpaces(string tagList)
        {
            if (tagList != null && tagList.Length == 0)
            {
                tagList = null;
            }

            Settings.RemoveSurroundingSpaces = tagList;
        }
    }
}
