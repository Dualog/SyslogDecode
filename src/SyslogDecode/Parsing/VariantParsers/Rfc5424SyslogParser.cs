﻿// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

namespace SyslogDecode.Parsing
{
    using System;
    using System.Collections.Generic;
    using SyslogDecode.Model;

    public class Rfc5424SyslogParser: ISyslogVariantParser
    {
        public bool TryParse(ParserContext ctx)
        {
            if (!ctx.Reset())
                return false;
            if(!ctx.Match("1 "))
            {
                return false; 
            }

            // It is RFC-5424 entry
            var entry = ctx.ParsedMessage; 
            entry.PayloadType = PayloadType.Rfc5424; 
            try
            {
                entry.Header = this.ParseHeader(ctx);
                entry.RawStructuredData5424 = this.ParseStructuredData(ctx);
                entry.Message = this.ParseMessage(ctx);
                return true; 
            }
            catch (Exception ex)
            {
                ctx.AddError(ex.Message);
                return false; 
            }
        }

        private  SyslogHeader ParseHeader(ParserContext ctx)
        {
            var header = ctx.ParsedMessage.Header = new SyslogHeader(); 
            header.Timestamp = ctx.ParseStandardTimestamp();
            header.HostName = ctx.ReadWordOrNil();
            header.AppName = ctx.ReadWordOrNil();
            header.ProcId = ctx.ReadWordOrNil();
            header.MsgId = ctx.ReadWordOrNil();
            return header; 
        }

        private string ParseStructuredData(ParserContext ctx)
        {
            ctx.SkipSpaces();

            if (ctx.Current == SyslogChars.NilChar)
            {
                ctx.Position++;
                return SyslogChars.NilChar.ToString(); 
            }

            var data = ctx.ParsedMessage.StructuredData5424;
            try
            {
                if (ctx.Current != SyslogChars.Lbr)
                {
                    // do not report it as an error, some messages out there are a bit malformed
                    // ctx.AddError("Expected [ for structured data.");
                    return SyslogChars.NilChar.ToString(); 
                }

                var structuredDataStartIndex = ctx.Position;
                
                // start parsing elements
                while(!ctx.Eof())
                {
                    var elem = ParseElement(ctx);
                    if (elem == null)
                    {
                        return ctx.Position == structuredDataStartIndex ?
                            SyslogChars.NilChar.ToString() :
                            ctx.Text.Substring(structuredDataStartIndex, ctx.Position - structuredDataStartIndex)
                                .Trim(SyslogChars.Space);
                    }
                    data[elem.Item1] = elem.Item2; 
                }

                return ctx.Position == structuredDataStartIndex ?
                    SyslogChars.NilChar.ToString() :
                    ctx.Text.Substring(structuredDataStartIndex, ctx.Position - structuredDataStartIndex)
                        .Trim(SyslogChars.Space);
            } catch (Exception ex)
            {
                ctx.AddError(ex.Message);
                return string.Empty;
            }
        }

        private  Tuple<string, List<NameValuePair>> ParseElement(ParserContext ctx)
        {
            if (ctx.Current != SyslogChars.Lbr)
            {
                return null; 
            }
            ctx.Position++;
            var elemName = ctx.ReadWord();
            ctx.SkipSpaces();
            var paramList = new List<NameValuePair>();
            Tuple<string, List<NameValuePair>> elem;
            
            if (ctx.Current != SyslogChars.EQ)
            {
                elem = new Tuple<string, List<NameValuePair>>(elemName, paramList);    
            }
            else
            {
                elem = new Tuple<string, List<NameValuePair>>(string.Empty, paramList);
                ctx.ReadSymbol('=');
                var paramValue = ctx.ReadQuotedString();
                var prm = new NameValuePair() { Name = elemName, Value = paramValue };
                paramList.Add(prm);
                ctx.SkipSpaces();
            }
            
            while (ctx.Current != SyslogChars.Rbr)
            {
                var paramName = ctx.ReadWord();
                ctx.ReadSymbol('=');
                var paramValue = ctx.ReadQuotedString();
                var prm = new NameValuePair() { Name = paramName, Value = paramValue };
                paramList.Add(prm);
                ctx.SkipSpaces();
            }

            ctx.ReadSymbol(SyslogChars.Rbr);
            return elem; 
        }

        private string ParseMessage(ParserContext ctx)
        {
            if (ctx.Eof())
            {
                return null;
            }
            var msg = ctx.Text.Substring(ctx.Position);
            msg = msg.TrimStart(SyslogChars.Space);
            // RFC 5424 allows BOM (byte order mark, 3 byte sequence) to precede the actual message. 
            // it will be read into the message OK, now 'msg' can contain this prefix - it is invisible
            // and will bring a lot of trouble when working with the string (ex: string comparisons are broken)
            // So we remove it explicitly.
            return msg.CutOffBOM();
        }


    } //class

}
