﻿using Mailjet.Client;
using Mailjet.Client.Resources;
using System;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Identity.UI.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Rocky_Utility
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration; 

        public MailJetSettings _mailJetSettings { get; set; }

        public EmailSender(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            return Execute(email, subject, htmlMessage);
        }

        public async Task Execute(string email, string subject, string body)
        {
            _mailJetSettings = _configuration.GetSection("MailJet").Get<MailJetSettings>();

            MailjetClient client = new MailjetClient(_mailJetSettings.ApiKey, _mailJetSettings.SecretKey);
            MailjetRequest request = new MailjetRequest
            {
                Resource = Send.Resource,
            }
            .Property(Send.Messages, new JArray {
                                            new JObject {
                                                { "From", new JObject {{"Email", "julianofalves@protonmail.com" }, {"Name", "Rocky"}}},
                                                {"To", new JArray {new JObject {{"Email", email }, { "Name", "DotNetMastery"}}}},
                                                {"Subject", subject},
                                                {"HTMLPart", body }
                                            }
             });
            MailjetResponse response = await client.PostAsync(request);
        }
    }
}
