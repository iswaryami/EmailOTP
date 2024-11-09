using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EmailOTP
{
    class Program
    {
        private const string AllowedDomain = ".dso.org.sg";
        private const int MaxAttempts = 10;
        private const int OtpExpirySeconds = 60;
        private string presentOtp;
        private DateTime otpTime;


        public void Start()
        {
            Console.WriteLine("Email OTP module task has been started");
        }

        public void Close()
        {
            Console.WriteLine("Email OTP module task has been ended");
        }

        /// <summary>
        /// This method is used to generate the otp and send email
        /// </summary>
        /// <param name="email">given email</param>
        /// <returns>status code</returns>
        public async Task<int> GenerateOtpEmailAsync(string email)
        {
            if (!IsValidEmail(email))
            {
                return StatusCodes.STATUS_EMAIL_INVALID;
            }

            if (!email.EndsWith(AllowedDomain))
            {
                return StatusCodes.STATUS_EMAIL_FAIL;
            }
            Random random = new Random();
            presentOtp= random.Next(100000, 999999).ToString();
            otpTime = DateTime.Now;
            string bodyEmail = $"Your OTP Code is {presentOtp}. The code is valid for 1 minute.";
            bool emailsend = await SendEmail(email, bodyEmail);
            return emailsend ? StatusCodes.STATUS_EMAIL_OK : StatusCodes.STATUS_EMAIL_FAIL;
        }

        /// <summary>
        /// This method is used to check the otp is incorrect , correct or timeout
        /// </summary>
        /// <param name="input">IOstream class</param>
        /// <returns>OTP Status Code</returns>
        public int CheckOtp(IOStream input)
        {
            int count = 0;
            var cts = new CancellationTokenSource();
            var token = cts.Token;
            try
            {
               while (count < MaxAttempts)
                {
                    string givenOtp = Task.Run(() => input.ReadOTP(), token).Result;

                    if (token.IsCancellationRequested)
                    {
                        return StatusCodes.STATUS_OTP_TIMEOUT;
                    }

                    count++;

                    if ((DateTime.Now - otpTime).TotalSeconds > OtpExpirySeconds)
                    {
                        return StatusCodes.STATUS_OTP_TIMEOUT;
                    }
                    else if (givenOtp == presentOtp)
                    {
                        return StatusCodes.STATUS_OTP_OK;
                    }
                    else
                    {
                        Console.WriteLine($"Incorrect OTP. Attempt {count}/10. Try again.");
                    }
                }

                return StatusCodes.STATUS_OTP_FAIL;
            }
            
            catch (OperationCanceledException)
            {
                return StatusCodes.STATUS_OTP_TIMEOUT;
            }

            finally
            {
                cts.Dispose();
            }
        }

        /// <summary>
        /// This methods helps to check whether the email is valid or not
        /// </summary>
        /// <param name="email">given email by user</param>
        /// <returns>returns true if the email is valid else false</returns>
        private bool IsValidEmail(string email)
        {
            var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
            return emailRegex.IsMatch(email);
        }

        /// <summary>
        /// This method helps to send email using smtp
        /// </summary>
        /// <param name="emailAddress">email address</param>
        /// <param name="body">body of the email</param>
        /// <returns></returns>
        private static async Task<bool> SendEmail(string email, string body)
        {
            try
            {
                var sendGridKey = ""; //replace with your sendapi
                var client = new SendGridClient(sendGridKey);
                var from = new EmailAddress("your3@mail.com", "TEST"); //replace with your email
                var subject = "OTP";
                var to = new EmailAddress(email);
                var plaintext = body;
                var htmlContent = body;
                var msg = MailHelper.CreateSingleEmail(from, to, subject, plaintext, htmlContent);

                var response = await client.SendEmailAsync(msg);

                if (response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode==System.Net.HttpStatusCode.Accepted)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }

        }

        static async Task Main(string[] args)
        {
        
            Program prog = new Program();
            prog.Start();

            Console.Write("Enter your email: ");
            string email = Console.ReadLine();
            int emailStatusCode = await prog.GenerateOtpEmailAsync(email);

            switch (emailStatusCode)
            {
                case StatusCodes.STATUS_EMAIL_OK:
                    Console.WriteLine("Email containing OTP has been sent successfully.");
                    break;
                case StatusCodes.STATUS_EMAIL_FAIL:
                    Console.WriteLine("Email address does not exist or sending to the email has failed.");
                    Environment.Exit(0);
                    break;
                case StatusCodes.STATUS_EMAIL_INVALID:
                    Console.WriteLine("Email address is invalid.");
                    Environment.Exit(0);  
                    break;
                    
            }

            IOStream ioStream = new IOStream();
            int otpStatusCode = prog.CheckOtp(ioStream);
            switch (otpStatusCode)
            {
                case StatusCodes.STATUS_OTP_OK:
                    Console.WriteLine("OTP is valid and checked");
                    break;
                case StatusCodes.STATUS_OTP_FAIL:
                    Console.WriteLine("OTP is wrong after 10 tries");
                    Environment.Exit(0);
                    break;
                case StatusCodes.STATUS_OTP_TIMEOUT:
                    Console.WriteLine("OTP timeout after 1 min");
                    Environment.Exit(0);
                    break;
            }

            prog.Close();
        }
}
}

