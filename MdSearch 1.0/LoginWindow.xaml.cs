using MailKit.Net.Smtp;
using MimeKit;
using System;
using System.Linq;
using System.Net.Mail;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace MdSearch_1._0
{
    public partial class LoginWindow : Window
    {
        private Entities entities;
        private string generatedCode;
        private Users currentUser;
        private DateTime codeGenerationTime;
        private const int CodeExpirationSeconds = 40;
        private DispatcherTimer verificationTimer;

        public LoginWindow()
        {
            InitializeComponent();

            try
            {
                entities = new Entities();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения к базе данных:\n{ex.Message}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        private void Enter_Click(object sender, RoutedEventArgs e)
        {
            string login = TBlogin.Text.Trim();
            string email = TBemail.Text.Trim();
            string password = PBpass.Password;

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                AuthMessage.Text = "Логин и пароль обязательны для заполнения";
                return;
            }

            currentUser = entities.Users.FirstOrDefault(u => u.Login == login);

            if (currentUser == null)
            {
                AuthMessage.Text = "Пользователь с таким логином не найден";
                return;
            }

            if (currentUser.RoleId != 2)
            {
                if (string.IsNullOrEmpty(email))
                {
                    AuthMessage.Text = "Email обязателен для заполнения";
                    return;
                }

                try
                {
                    var mailAddress = new MailAddress(email);
                }
                catch
                {
                    AuthMessage.Text = "Введите корректный email";
                    return;
                }

                if (!email.Equals(currentUser.Email, StringComparison.OrdinalIgnoreCase))
                {
                    AuthMessage.Text = "Указанный email не соответствует email пользователя";
                    return;
                }
            }

            if (!VerifyPassword(password, currentUser.PasswordHash))
            {
                AuthMessage.Text = "Неверный пароль";
                return;
            }

            if (currentUser.RoleId == 2)
            {
                MainWindow mainWindow = new MainWindow(currentUser.RoleId);
                mainWindow.Show();
                this.Close();
            }
            else
            {
                generatedCode = GenerateRandomCode();
                codeGenerationTime = DateTime.Now;
                SendVerificationCode(email, generatedCode);
                ShowVerificationControls();
            }
        }

        private void Verify_Click(object sender, RoutedEventArgs e)
        {
            string enteredCode = TBverificationCode.Text.Trim();

            if (string.IsNullOrEmpty(enteredCode))
            {
                AuthMessage.Text = "Введите код подтверждения";
                AuthMessage.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            TimeSpan timeDifference = DateTime.Now - codeGenerationTime;
            if (timeDifference.TotalSeconds > CodeExpirationSeconds)
            {
                AuthMessage.Text = "Срок действия кода истек. Зайдите снова для получения нового";
                AuthMessage.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            if (enteredCode == generatedCode)
            {
                MainWindow mainWindow = new MainWindow(currentUser.RoleId);
                mainWindow.Show();
                this.Close();
            }
            else
            {
                AuthMessage.Text = "Неверный код подтверждения";
                AuthMessage.Foreground = System.Windows.Media.Brushes.Red;
            }
        }
        private void ShowVerificationControls()
        {
            TBlogin.IsEnabled = false;
            TBemail.IsEnabled = false;
            PBpass.IsEnabled = false;
            BtnEnter.Visibility = Visibility.Collapsed;

            LabelVerificationCode.Visibility = Visibility.Visible;
            TBverificationCode.Visibility = Visibility.Visible;
            BtnVerify.Visibility = Visibility.Visible;

            AuthMessage.Text = "Код подтверждения отправлен на вашу почту";
            AuthMessage.Foreground = System.Windows.Media.Brushes.LightGreen;

            // Инициализация таймера
            if (verificationTimer == null)
            {
                verificationTimer = new DispatcherTimer();
                verificationTimer.Interval = TimeSpan.FromSeconds(1);
                verificationTimer.Tick += Timer_Tick;
            }

            verificationTimer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            TimeSpan remainingTime = codeGenerationTime.AddSeconds(CodeExpirationSeconds) - DateTime.Now;

            if (remainingTime.TotalSeconds <= 0)
            {
                verificationTimer.Stop();
                TimerText.Text = "Время истекло";
                TimerText.Foreground = Brushes.Red;
            }
            else
            {
                TimerText.Text = $"Осталось времени: {remainingTime.Seconds} сек.";
            }
        }

        private string GenerateRandomCode()
        {
            Random random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        private void SendVerificationCode(string recipientEmail, string code)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("MdSearch", "mdsearch46@gmail.com"));
                message.To.Add(new MailboxAddress("", recipientEmail));
                message.Subject = "Код подтверждения для входа";

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #ddd; border-radius: 10px; background-color: #f9f9f9;'>
                    <h2 style='color: #333;'>Код подтверждения для входа</h2>
                    <p>Здравствуйте,</p>
                    <p>Для завершения входа в ваш аккаунт используйте следующий код:</p>
                    <div style='text-align: center; margin: 20px 0;'>
                        <span style='display: inline-block; font-size: 24px; font-weight: bold; letter-spacing: 5px; color: #27ae60; background-color: #d5f5e3; padding: 10px 20px; border-radius: 8px;'>
                            {code}
                        </span>
                    </div>
                    <p>Этот код действителен в течение 40 секунд.</p>
                    <p>Если вы не запрашивали код, проигнорируйте это письмо.</p>
                    <hr style='margin-top: 20px; margin-bottom: 10px;' />
                    <p style='font-size: 13px; color: #777;'>© 2025 MdSearch. Все права защищены</p>
                </div>"
                };

                message.Body = bodyBuilder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    client.Connect("smtp.gmail.com", 465, true);
                    client.Authenticate("mdsearch46@gmail.com", "jqzz oltd wfxp glqp");
                    client.Send(message);
                    client.Disconnect(true);
                }
            }
            catch (SmtpCommandException smtpEx)
            {
                MessageBox.Show(
                    $"SMTP Ошибка:\nКод: {smtpEx.StatusCode}\nОшибка: {smtpEx.Message}\n" +
                    "Ошибка:\n" +
                    "Блокировка Google\n",
                    "Ошибка SMTP", MessageBoxButton.OK, MessageBoxImage.Error
                );
            }
            catch (AuthenticationException authEx)
            {
                MessageBox.Show(
                    $"Ошибка аутентификации:\n{authEx.Message}\n" +
                    "Причина:\n" +
                    "Неправильный пароль приложения\n",
                    "Ошибка входа", MessageBoxButton.OK, MessageBoxImage.Error
                );
            }
            catch (SocketException socketEx)
            {
                MessageBox.Show(
                    $"Сетевая ошибка:\n{socketEx.Message}\n" +
                    "Возможно:\n" +
                    "1. Нет интернета\n" +
                    "2. Порт 465 заблокирован (попробуйте VPN)\n" +
                    "3. Антивирус/фаервол блокирует соединение",
                    "Ошибка сети", MessageBoxButton.OK, MessageBoxImage.Error
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.GetType().Name}\nПопробуйте ещё раз.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool VerifyPassword(string password, string passwordHash)
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
    }
}