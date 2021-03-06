﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using PipelineNet.Middleware;
using Serilog;
using Texnomic.DNS.Abstractions;
using Texnomic.DNS.Abstractions.Enums;
using Texnomic.DNS.Models;
using Texnomic.DNS.Servers.Extensions;
using Texnomic.DNS.Servers.Options;
using Texnomic.FilterLists;
using Texnomic.FilterLists.Enums;
using Texnomic.FilterLists.Models;

namespace Texnomic.DNS.Servers.Middlewares
{
    public class FilterListsMiddleware : IAsyncMiddleware<IMessage, IMessage>
    {
        private readonly ILogger Logger;
        private readonly WebClient WebClient;
        private readonly FastHashSet<string> Filter;

        public FilterListsMiddleware(IOptionsMonitor<FilterListsMiddlewareOptions> Options, ILogger Logger) : base()
        {
            this.Logger = Logger;

            WebClient = new WebClient();

            Filter = new FastHashSet<string>();

            Options.OnChange(OptionsOnChange);

            _ = InitializeAsync(Options.CurrentValue.IDs);
        }

        private void OptionsOnChange(FilterListsMiddlewareOptions Options)
        {
            Filter.Clear();

            Filter.TrimExcess();

            _ = InitializeAsync(Options.IDs);

            Logger.Information("FilterLists {@IDs} Updates Applied.", Options.IDs);
        }

        public async Task<IMessage> Run(IMessage Message, Func<IMessage, Task<IMessage>> Next)
        {
            if (Filter.Contains(Message.Questions[0].Domain.Name))
            {
                Logger.Warning("Filtered Query {@ID} To {@Domain}.", Message.ID, Message.Questions[0].Domain.Name);

                return new Message()
                {
                    ID = Message.ID,
                    MessageType = MessageType.Response,
                    ResponseCode = ResponseCode.Refused,
                    Questions = Message.Questions
                };
            }
            else
            {
                return await Next(Message);
            }
        }

        public async Task InitializeAsync(int[] IDs)
        {
            try
            {
                Logger.Verbose("FilterLists Initialization Started.");

                var Lists = await GetFilterListsAsync(IDs);

                Logger.Information("FilterLists Initialization Started with {@Count} Selected Lists.", string.Format("{0:n0}", Lists.Count));

                string File = null;
                string[] Domains = null;

                foreach (var List in Lists)
                {
                    try
                    {
                        File = await WebClient.DownloadStringTaskAsync(List.ViewUrl);

                        Domains = Parse(File);

                        for (int i = 0; i < Domains.Length; i++)
                        {
                            if (!Filter.Contains(Domains[i]))
                            {
                                Filter.Add(Domains[i]);
                            }
                        }

                        Logger.Verbose("{@FilterList} Download Completed.", List);
                    }
                    catch (Exception Error)
                    {
                        Logger.Error("{@Error} While Downloading {@FilterList}.", Error, List);
                    }
                }

                Filter.TrimExcess();

                File = null;
                Domains = null;
                Lists = null;

                Logger.Information("FilterLists Initialization Completed with {@Count} Domains.", string.Format("{0:n0}", Filter.Count));
            }
            catch (Exception Error)
            {
                Logger.Fatal("Fatal {@Error} Occurred While Initializing FilterLists.", Error);
            }
        }

        private static async Task<List<FilterList>> GetFilterListsAsync(int[] IDs)
        {
            var Client = new FilterListsClient();

            var Lists = await Client.GetListsAsync();

            Lists = Lists.Where(List => List.Syntax == Syntax.Hosts)
                         .Where(List => IDs.Contains(List.ID))           
                         .ToList();

            return Lists;
        }

        private static string[] Parse(string File)
        {
            return File.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                       .Select(Line => Line.Replace("0.0.0.0", ""))
                       .Select(Line => Line.Replace("127.0.0.1", ""))
                       .Select(Line => Line.Trim())
                       .SkipWhile(Line => Line.StartsWith("#"))
                       .SkipWhile(Line => string.IsNullOrEmpty(Line))
                       .SkipWhile(Line => string.IsNullOrWhiteSpace(Line))
                       .ToArray();
        }
    }
}
