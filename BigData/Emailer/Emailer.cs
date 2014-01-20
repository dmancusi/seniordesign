using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Mail;
using System.Xml;
using Newtonsoft.Json;
using Nustache.Core;
using System.Windows.Media.Imaging;
using System.IO;

namespace BigData.Emailer {

    /// <summary>
    /// Manages sending emails to users and resolving usernames to full names.
    /// </summary>
    public class Emailer {

        /// <summary>
        /// Sends follow-up email to Library patron
        /// </summary>
        /// <param name="username">Bucknell username of patron</param>
        /// <param name="pub">Pulication instance to follow up</param>
        public static async void emailSend(string username, Publication pub) {
            var fromAddress = new MailAddress(Properties.Settings.Default.MailFrom, Properties.Settings.Default.MailName);
            MailAddress toAddress;
            try {
                toAddress = new MailAddress(username + "@bucknell.edu", await getFullName(username));
            }
            catch (Exception) {
                Console.WriteLine("Invalid username. Email not sent.");
                return;
            }
            string fromPassword = Properties.Settings.Default.MailPassword;
            string subject = "Here is your eBook!: " + pub.Title;

            MemoryStream str = new MemoryStream();
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(pub.CoverImage));
            encoder.Save(str);
            str.Position = 0;

            var coverInline = new LinkedResource(str, "image/png");
            string body = getMessageBody(await getFirstName(username), pub, coverInline);

            var smtp = new SmtpClient {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
            };
            using (var message = new MailMessage(fromAddress, toAddress) {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            }) {
                try {
                    var view = AlternateView.CreateAlternateViewFromString(body, null, "text/html");
                    view.LinkedResources.Add(coverInline);
                    message.AlternateViews.Add(view);
                    await smtp.SendMailAsync(message);
                    Console.WriteLine("Sent to " + toAddress);
                } catch (Exception e) {
                    Console.WriteLine("Error: " + e);
                }
            }
        }

        /// <summary>
        /// Returns the full name of a person in the Bucknell directory
        /// </summary>
        /// <param name="username">Bucknell username</param>
        /// <returns>Full name associated with username</returns>
        static async Task<string> getFullName(String username) {
            var uri = new Uri(@"https://m.bucknell.edu/mobi-web/api/?module=people&q=" + username);
            var request = WebRequest.CreateHttp(uri);
            var response = await request.GetResponseAsync();
            
            var sr = new StreamReader(response.GetResponseStream());
            string json = await sr.ReadToEndAsync();
            List<dynamic> result = JsonConvert.DeserializeObject<List<dynamic>>(json);
            String name = result.First().givenname[0];
            return name;
        }

        /// <summary>
        /// Returns the first name of a person in the Bucknell directory
        /// </summary>
        /// <param name="username">Bucknell username</param>
        /// <returns>First name associated with username</returns>
        static async Task<string> getFirstName(String username) {
            var uri = new Uri(@"https://m.bucknell.edu/mobi-web/api/?module=people&q=" + username);
            var request = WebRequest.CreateHttp(uri);
            var response = await request.GetResponseAsync();

            var sr = new StreamReader(response.GetResponseStream());
            string json = await sr.ReadToEndAsync();
            List<dynamic> result = JsonConvert.DeserializeObject<List<dynamic>>(json);
            String name = result.First().givenname[0];
            name = name.Split(' ')[0];
            return name;
        }

        /// <summary>
        /// Provides the body of a nicely formatted follow-up email
        /// </summary>
        /// <param name="name">The name of the patron requesting the email</param>
        /// <param name="pub">The Publication instance to be advertised</param>
        /// <param name="cover">The cover image of the publications</param>
        /// <returns>HTML body of email</returns>
        static string getMessageBody(String name, Publication pub, LinkedResource cover) {
            string sTemplate = @"
<p>{{name}},</p>
<p><a href='{{link}}'>Click here</a> to borrow {{pubname}}.</p>
<div><a href='{{link}}'><img src='cid:{{coverURI}}' /></a></div>
<p><a href='{{link2}}'>More information on Bucknell eBooks</a></p>";
            Dictionary<string, string> data = new Dictionary<string, string>();
            data["name"] = name;
            data["pubname"] = pub.Title;
            data["link"] = "https://bucknell.worldcat.org/oclc/" + pub.OCLCNumber;
            data["link2"] = "http://researchbysubject.bucknell.edu/ebooks";
            data["coverURI"] = cover.ContentId;
            return Nustache.Core.Render.StringToString(sTemplate, data);
        }
    }
}
