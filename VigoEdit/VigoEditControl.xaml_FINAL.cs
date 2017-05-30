using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace VigoEdit
{
    // OBSERVAÇÃO IMPORTANTE
    //
    // Ao usar o PasswordChar para ocultar os caracteres que estão sendo digitados, na camada do aplicativo deve-se usar a propriedade
    // TextoOriginal ao invés da Texto, pois Texto retornará ***** enquanto que TextoOriginal retornará o conteúdo real do campo.
    //=====================================================================================================================================

    // Converter (true = Visible) e (false = Hidden) para exibição do botão (recurso nativo do WPF)
    //---------------------------------------------------------------------------------------------

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value == true)
                return "Visible";
            else
                return "Hidden";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((string)value == "Visible")
                return true;
            else
                return false;
        }
    }

    // TextBox com máscara
    //--------------------

    #region Máscara

    [ContentProperty("Text")]
    [Localizability(LocalizationCategory.Text)]
    public class MaskedTextBox : TextBox
    {
        private MaskedTextProvider maskProvider;
        public string textoOriginal;

        public static readonly DependencyProperty MaskProperty = DependencyProperty.Register("Mask", typeof(string), typeof(MaskedTextBox), new UIPropertyMetadata(OnMaskPropertyChanged));
        public string Mask
        {
            get { return (string)this.GetValue(MaskProperty); }
            set { this.SetValue(MaskProperty, value); }
        }

        public static readonly DependencyProperty PasswordCharProperty = DependencyProperty.Register("PasswordChar", typeof(char), typeof(MaskedTextBox), new UIPropertyMetadata(OnMaskPropertyChanged));
        public char PasswordChar
        {
            get { return (char)this.GetValue(PasswordCharProperty); }
            set { this.SetValue(PasswordCharProperty, value); }
        }

        // Executar pressionamento de teclas
        //----------------------------------

        public static void SendKey(Key key)
        {
            if (Keyboard.PrimaryDevice != null)
            {
                if (Keyboard.PrimaryDevice.ActiveSource != null)
                {
                    var e = new KeyEventArgs(Keyboard.PrimaryDevice, Keyboard.PrimaryDevice.ActiveSource, 0, key)
                    {
                        RoutedEvent = Keyboard.KeyDownEvent
                    };

                    InputManager.Current.ProcessInput(e);
                }
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if ((Mask != "") && (Mask != null))
            {
                var position = this.SelectionStart;
                var selectionLength = this.SelectionLength;

                switch (e.Key)
                {
                    case Key.Back:
                        if (selectionLength == 0)
                            this.RemoveChar(this.GetEditPositionTo(--position));
                        else
                            this.RemoveRange(position, selectionLength);

                        e.Handled = true;
                        break;

                    case Key.Delete:
                        if (selectionLength == 0)
                            this.RemoveChar(this.GetEditPositionFrom(position));
                        else
                            this.RemoveRange(position, selectionLength);

                        e.Handled = true;
                        break;

                    case Key.Space:
                        if (selectionLength != 0 && this.IsValidKey(e.Key, position))
                            this.RemoveRange(position, selectionLength);
                        else
                            this.UpdateText(" ", position);

                        e.Handled = true;
                        break;

                    default:
                        if (selectionLength != 0 && this.IsValidKey(e.Key, position))
                            this.RemoveRange(position, selectionLength);

                        break;
                }
            }

            // <enter> funcionar como <tab> ** SEMPRE **

            if (e.Key == Key.Enter)
                SendKey(Key.Tab);
        }

        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            if ((Mask != "") && (Mask != null))
            {
                e.Handled = true;

                if (!this.IsReadOnly)
                {
                    var position = this.SelectionStart;
                    position = UpdateText(e.Text, position);
                    base.OnPreviewTextInput(e);
                }
            }
        }

        private static void OnMaskPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (MaskedTextBox)d;

            if ((control.Mask != "") && (control.Mask != null))
            {
                control.maskProvider = new MaskedTextProvider(control.Mask) { ResetOnSpace = false };
                control.maskProvider.PasswordChar = control.PasswordChar;
                control.maskProvider.Set(control.Text);
                control.RefreshText(control.SelectionStart);
            }
        }

        private int GetEditPositionFrom(int startPosition)
        {
            var position = this.maskProvider.FindEditPositionFrom(startPosition, true);
            return position == -1 ? startPosition : position;
        }

        private int GetEditPositionTo(int endPosition)
        {
            while (endPosition >= 0 && !this.maskProvider.IsEditPosition(endPosition))
                endPosition--;

            return endPosition;
        }

        private bool IsValidKey(Key key, int position)
        {
            char virtualKey = (char)KeyInterop.VirtualKeyFromKey(key);
            MaskedTextResultHint resultHint;
            return this.maskProvider.VerifyChar(virtualKey, position, out resultHint);
        }

        private void RefreshText(int position)
        {
            if (Mask.Replace("&", "") == "")
                this.maskProvider.PromptChar = Convert.ToChar(" ");
            else
                this.maskProvider.PromptChar = Convert.ToChar("_");

            if (!this.IsFocused)
                this.Text = this.maskProvider.ToString(true, true);
            else
                this.Text = this.maskProvider.ToDisplayString();

            // Texto original para armazenar o conteúdo real (caso seja usado o PasswordChar o conteúdo ficaria sendo somente "*")

            textoOriginal = this.maskProvider.ToString(true);

            this.SelectionStart = position;
        }

        private void RemoveRange(int position, int selectionLength)
        {
            if (this.maskProvider.RemoveAt(position, position + selectionLength - 1))
                this.RefreshText(position);
        }

        private void RemoveChar(int position)
        {
            if (this.maskProvider.RemoveAt(position))
                this.RefreshText(position);
        }

        private int UpdateText(string text, int position)
        {
            if (position < this.Text.Length)
            {
                position = this.GetEditPositionFrom(position);

                if ((Keyboard.IsKeyToggled(Key.Insert) && this.maskProvider.Replace(text, position)) ||
                    this.maskProvider.InsertAt(text, position))
                    position++;

                position = this.GetEditPositionFrom(position);
            }

            this.RefreshText(position);
            return position;
        }
    }

    #endregion

    // VigoEditControl (Caixa, legenda, texto e botão)
    //------------------------------------------------

    public partial class VigoEditControl : UserControl
    {
        bool FValidado = true;
        public string TextoOriginal;

        // Expressões regulares para validação posterior
        //----------------------------------------------

        Regex REGEX_MAIL = new Regex(@"^[A-Za-z0-9_](([_\.\-]?[a-zA-Z0-9_-]+)*)@([A-Za-z0-9]+)(([\.\-]?[a-zA-Z0-9]+)*)\.([A-Za-z]{2,})$");
        Regex REGEX_IP = new Regex(@"\b(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b");
        Regex REGEX_MAC = new Regex(@"^([0-9a-fA-F][0-9a-fA-F]:){5}([0-9a-fA-F][0-9a-fA-F])$");
        Regex REGEX_CEP = new Regex(@"^[0-9]{5}-[0-9]{3}$");
        Regex REGEX_TELEFONE = new Regex(@"^\(\d{2}\)[0-9 \s]\d{4}-\d{4}$");
        Regex REGEX_HORA = new Regex(@"^(?:0?[0-9]|1[0-9]|2[0-3]):[0-5][0-9]$");
        Regex REGEX_CONTA = new Regex(@"^[0-9]{2}.[0-9]{3}.[0-9]{3}-[0-9XxPp]{1}$");
        Regex REGEX_IP6 = new Regex(@"^(((?=.*(::))(?!.*\3.+\3))\3?|[0-9A-F]{1,4}:)([0-9A-F]{1,4}(\3|:\b)|\2){5}(([0-9A-F]{1,4}(\3|:\b|$)|\2){2}|(((2[0-4]|1[0-9]|[1-9])?[0-9]|25[0-5])\.?\b){4})\z");

        // Funções privadas
        //-----------------

        private bool ValidaCPF(string cpf)
        {
            try
            {
                int[] multiplicador1 = new int[9] { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
                int[] multiplicador2 = new int[10] { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

                string tempCpf;
                string digito;

                int soma;
                int resto;

                cpf = cpf.Replace(".", "").Replace("-", "").Trim();

                if ((cpf.Length != 11) ||
                    (cpf == "11111111111") ||
                    (cpf == "22222222222") ||
                    (cpf == "33333333333") ||
                    (cpf == "44444444444") ||
                    (cpf == "55555555555") ||
                    (cpf == "66666666666") ||
                    (cpf == "77777777777") ||
                    (cpf == "88888888888") ||
                    (cpf == "99999999999"))
                    return false;

                tempCpf = cpf.Substring(0, 9);
                soma = 0;

                for (int i = 0; i < 9; i++)
                    soma += int.Parse(tempCpf[i].ToString()) * multiplicador1[i];

                resto = soma % 11;

                if (resto < 2)
                    resto = 0;
                else
                    resto = 11 - resto;

                digito = resto.ToString();
                tempCpf = tempCpf + digito;
                soma = 0;

                for (int i = 0; i < 10; i++)
                    soma += int.Parse(tempCpf[i].ToString()) * multiplicador2[i];

                resto = soma % 11;

                if (resto < 2)
                    resto = 0;
                else
                    resto = 11 - resto;

                digito = digito + resto.ToString();
                return cpf.EndsWith(digito);
            }
            catch
            {
                return false;
            }
        }

        private bool ValidaCNPJ(string cnpj)
        {
            try
            {
                int[] multiplicador1 = new int[12] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
                int[] multiplicador2 = new int[13] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

                int soma;
                int resto;

                string digito;
                string tempCnpj;

                cnpj = cnpj.Trim();
                cnpj = cnpj.Replace(".", "").Replace("-", "").Replace("/", "");

                if (cnpj.Length != 14)
                    return false;

                tempCnpj = cnpj.Substring(0, 12);
                soma = 0;

                for (int i = 0; i < 12; i++)
                    soma += int.Parse(tempCnpj[i].ToString()) * multiplicador1[i];

                resto = (soma % 11);

                if (resto < 2)
                    resto = 0;
                else
                    resto = 11 - resto;

                digito = resto.ToString();
                tempCnpj = tempCnpj + digito;
                soma = 0;

                for (int i = 0; i < 13; i++)
                    soma += int.Parse(tempCnpj[i].ToString()) * multiplicador2[i];

                resto = (soma % 11);

                if (resto < 2)
                    resto = 0;
                else
                    resto = 11 - resto;

                digito = digito + resto.ToString();

                return cnpj.EndsWith(digito);
            }
            catch
            {
                return false;
            }
        }

        private bool ValidaDATA(string texto)
        {
            try
            {
                Convert.ToDateTime(Texto);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool ValidaNumero(string texto)
        {
            try
            {
                Convert.ToInt64(Texto);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool ValidaValor(string texto)
        {
            try
            {
                Convert.ToDecimal(Texto);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool ValidaUF(string texto)
        {
            return ("SPMGRJRSSCPRESDFMTMSGOTOBASEALPBPEMARNCEPIPAAMAPFNACRRRO".IndexOf(texto) % 2 == 0) ? true : false;
        }

        // Delegate do botão caso uma ação seja definida
        //----------------------------------------------

        public delegate void ClickBotao();
        public event ClickBotao Click_Botao;
        public void ExecutaBotao(object sender, RoutedEventArgs e)
        {
            if (Click_Botao != null)
                Click_Botao();
        }

        private string RetiraAcentos(string texto)
        {
            string comAcentos = "ÄÅÁÂÀÃäáâàãÉÊËÈéêëèÍÎÏÌíîïìÖÓÔÒÕöóôòõÜÚÛüúûùÇç&";
            string semAcentos = "AAAAAAaaaaaEEEEeeeeIIIIiiiiOOOOOoooooUUUuuuuCce";

            for (int i = 0; i < comAcentos.Length; i++)
                texto = texto.Replace(comAcentos[i].ToString(), semAcentos[i].ToString());

            return texto;
        }

        private void AjustaPropriedades()
        {
            string m = Mascara;

            SetValue(MascaraProperty, ""); // Deixar em branco para forçar o remanejamento para os campos pré-definidos
            SetValue(TamanhoProperty, Tamanho);
            textbox.PasswordChar = (char)0;

            if (Tipo == "Data")
                SetValue(MascaraProperty, "&0/00/0000");

            else if (Tipo == "MAC")
                SetValue(MascaraProperty, "AA:AA:AA:AA:AA:AA");

            else if (Tipo == "CPF")
                SetValue(MascaraProperty, "000,000,000-00");

            else if (Tipo == "CNPJ")
                SetValue(MascaraProperty, "00,000,000/0000-00");

            else if (Tipo == "CEP")
                SetValue(MascaraProperty, "00000-000");

            else if (Tipo == "Telefone")
                SetValue(MascaraProperty, "(00)&0000-0000");

            else if (Tipo == "Conta")
                SetValue(MascaraProperty, "00,000,000-A");

            else if (Tipo == "Hora")
                SetValue(MascaraProperty, "00:00");

            else if (Tipo == "UF")
                SetValue(TamanhoProperty, 2);

            else if (Tipo == "Senha")
            {
                SetValue(MascaraProperty, "&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&");
                SetValue(TamanhoProperty, 50);
                textbox.PasswordChar = '*';
            }
            else if (m.Trim() != "") // Caso possua máscara mas não é um campo pré-definido
            {
                // Remove a máscara e coloca de novo pra dar um refresh no MaskedTextBox (OnMaskChanged)

                textbox.Mask = "";
                textbox.Mask = m;
                SetValue(MascaraProperty, m);
            }
        }

        // Código do UserControl
        //----------------------

        public VigoEditControl()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            AjustaGeral();
        }

        private void AjustaGeral()
        {
            AjustaBordas(); // Aqui para ajustar as bordas assim que o usercontrol é renderizado

            // Tamanho

            textbox.MaxLength = Tamanho;

            // Alinhamento

            textbox.TextAlignment = Alinhamento;

            // Máscara

            AjustaPropriedades();

            // Troca de máscara apenas para atualizar o usercontrol (gambiarra)

            if ((Mascara != "") & (Mascara != null) & (Tipo == "Senha"))
            {
                textbox.Mask = "&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&";
                textbox.Mask = Mascara;
            }
        }

        private void UserControl_GotFocus(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            AjustaGeral();

            // Azul clarinho

            SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(200, 220, 236, 249));
            rectangle.Fill = brush;

            // Seleciona todo o conteúdo caso exista algo

            textbox.Focus();

            if (textbox.Text.Trim().Length > 0)
                textbox.SelectAll();
        }

        private void UserControl_LostFocus(object sender, RoutedEventArgs e)
        {
            // "Pintar" de branco ou vermelho conforme resultado da validação

            if (!ValidarConteudo())
            {
                rectangle.Fill = Brushes.MistyRose;
                FValidado = false;
            }
            else
            {
                rectangle.Fill = Brushes.White;
                FValidado = true;
            }

            // Formatar campo de VALOR

            if (Tipo == "Valor")
            {
                try
                {
                    textbox.Text = String.Format("{0:0.00}", Convert.ToDecimal(textbox.Text)).Trim();
                }
                catch
                {
                    textbox.Text = "0,00";
                }

                this.Texto = textbox.Text;
            }
        }

        // Propriedade para ESCOLHER o tipo, somente dos que existem, não sendo permitido "digitar qualquer coisa"
        //--------------------------------------------------------------------------------------------------------

        public static readonly DependencyProperty TipoProperty = DependencyProperty.Register("Tipo", typeof(string), typeof(VigoEditControl), new UIPropertyMetadata("Normal", OnControlPropertyChanged));

        private static void OnControlPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (VigoEditControl)d;
            control.AjustaGeral();
        }

        [TypeConverter(typeof(tcCampo))]
        public string Tipo
        {
            get { return (string)GetValue(TipoProperty); }
            set
            {
                SetValue(TipoProperty, value);
                AjustaGeral();
            }
        }

        public class tcCampo : StringConverter
        {
            public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
            {
                return true;
            }
            public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
            {
                return new StandardValuesCollection(new string[] { "Normal", "Numero", "Data", "Valor", "E-mail", "IP", "MAC", "CPF", "CNPJ", "CEP", "Senha", "Telefone", "Conta", "Hora", "UF", "IPv6" });
            }
        }

        //--------------------------------------------------------------------------------------------------------

        public static readonly DependencyProperty BordaProperty = DependencyProperty.Register("Borda", typeof(Thickness), typeof(VigoEditControl), new FrameworkPropertyMetadata(new Thickness(1)));
        public Thickness Borda
        {
            get { return (Thickness)GetValue(BordaProperty); }
            set
            {
                SetValue(BordaProperty, value);
                borda.BorderThickness = value;
            }
        }

        public static readonly DependencyProperty LegendaProperty = DependencyProperty.Register("Legenda", typeof(string), typeof(VigoEditControl), new FrameworkPropertyMetadata("Legenda"));
        public string Legenda
        {
            get { return (string)GetValue(LegendaProperty); }
            set
            {
                SetValue(LegendaProperty, value);
                subtitle.Content = value;
            }
        }

        public static readonly DependencyProperty MascaraProperty = DependencyProperty.Register("Mascara", typeof(string), typeof(VigoEditControl), new FrameworkPropertyMetadata(""));
        public string Mascara
        {
            get { return (string)GetValue(MascaraProperty); }
            set
            {
                SetValue(MascaraProperty, value);
                textbox.Mask = value;
            }
        }

        public static readonly DependencyProperty TextoProperty = DependencyProperty.Register("Texto", typeof(string), typeof(VigoEditControl), new FrameworkPropertyMetadata(""));
        public string Texto
        {
            get { return (string)GetValue(TextoProperty); }
            set {
                SetValue(TextoProperty, value);
                textbox.Text = value;
                TextoOriginal = textbox.textoOriginal;
            }
        }

        public static readonly DependencyProperty TamanhoProperty = DependencyProperty.Register("Tamanho", typeof(int), typeof(VigoEditControl), new FrameworkPropertyMetadata(0));
        public int Tamanho
        {
            get { return (int)GetValue(TamanhoProperty); }
            set
            {
                SetValue(TamanhoProperty, value);
                textbox.MaxLength = value; // Aqui só terá efeito caso a mudança seja feita em tempo real (via código)
            }
        }

        public static readonly DependencyProperty AlinhamentoProperty = DependencyProperty.Register("Alinhamento", typeof(TextAlignment), typeof(VigoEditControl), new FrameworkPropertyMetadata(TextAlignment.Left));
        public TextAlignment Alinhamento
        {
            get { return (TextAlignment)GetValue(AlinhamentoProperty); }
            set
            {
                SetValue(AlinhamentoProperty, value);
                textbox.TextAlignment = value; // Aqui só terá efeito caso a mudança seja feita em tempo real (via código)
            }
        }

        public static readonly DependencyProperty BotaoProperty = DependencyProperty.Register("ExibeBotao", typeof(bool), typeof(VigoEditControl), new FrameworkPropertyMetadata(false));
        public bool ExibeBotao
        {
            get { return (bool)GetValue(BotaoProperty); }
            set
            {
                SetValue(BotaoProperty, value);
                AjustaBordas(); // Aqui só terá efeito caso a mudança seja feita em tempo real (via código)
            }
        }

        public static readonly DependencyProperty PodeSerNuloProperty = DependencyProperty.Register("PodeSerNulo", typeof(bool), typeof(VigoEditControl), new FrameworkPropertyMetadata(true));
        public bool PodeSerNulo
        {
            get { return (bool)GetValue(PodeSerNuloProperty); }
            set { SetValue(PodeSerNuloProperty, value); }
        }

        public static readonly DependencyProperty PodeTerMenosProperty = DependencyProperty.Register("PodeTerMenos", typeof(bool), typeof(VigoEditControl), new FrameworkPropertyMetadata(true));
        public bool PodeTerMenos
        {
            get { return (bool)GetValue(PodeTerMenosProperty); }
            set { SetValue(PodeTerMenosProperty, value); }
        }

        public static readonly DependencyProperty PermiteAcentuacaoProperty = DependencyProperty.Register("PermiteAcentuacao", typeof(bool), typeof(VigoEditControl), new FrameworkPropertyMetadata(true));
        public bool PermiteAcentuacao
        {
            get { return (bool)GetValue(PermiteAcentuacaoProperty); }
            set { SetValue(PermiteAcentuacaoProperty, value); }
        }

        public static readonly DependencyProperty ValidadoProperty = DependencyProperty.Register("Validado", typeof(bool), typeof(VigoEditControl), new FrameworkPropertyMetadata(true));
        public bool Validado
        {
            get { return FValidado; }
        }

        private void AjustaBordas()
        {
            Thickness borda1 = new Thickness();
            Thickness borda2 = new Thickness();

            // Borda do LABEL "2,1,5,0"

            borda1.Left = 2;
            borda1.Top = 1;
            borda1.Right = 5;
            borda1.Bottom = 0;

            // Borda do TEXTBOX "5,0,5,5"

            borda2.Left = 5;
            borda2.Top = 0;
            borda2.Right = 5;
            borda2.Bottom = 5;

            // Usa as margens corretas de acordo com a exibição do botão

            if (button.Visibility == Visibility.Hidden)
            {
                borda1.Right = 5;
                borda2.Right = 5;

                subtitle.Margin = borda1;
                textbox.Margin = borda2;
            }
            else
            {
                borda1.Right = 50;
                borda2.Right = 50;

                subtitle.Margin = borda1;
                textbox.Margin = borda2;
            }
        }

        private bool ValidarConteudo()
        {
            // Remover o "lixo" oculto (caracteres invisíveis, unicodes, soft-hifen, etc)

            try
            {
                TextoOriginal = new string(textbox.textoOriginal.Trim().Where(c => char.IsLetter(c) || char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsDigit(c)).ToArray());
                TextoOriginal = TextoOriginal.Replace("–", "-"); // Remover o soft-hifen
            }
            catch { TextoOriginal = ""; }

            try
            {
                // Remove acentuação caso não seja permitido

                if (!PermiteAcentuacao)
                    textbox.Text = RetiraAcentos(textbox.Text);

                // Remover o "lixo" oculto (caracteres invisíveis, unicodes, soft_hifen, etc)

                textbox.Text = new string(textbox.Text.Trim().Where(c => char.IsLetter(c) || char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsDigit(c)).ToArray());
                textbox.Text = textbox.Text.Replace("–", "-"); // Remover o soft-hifen

                // Coloca em maiúsculo caso seja UF

                if (Tipo == "UF")
                    textbox.Text = textbox.Text.ToUpper();

                this.Texto = textbox.Text;

                // Validar o conteúdo de acordo com o tipo

                if ((PodeSerNulo) && ((Texto.Replace("_", "").Trim() == "") || (Texto.Replace("_", "").Trim() == null) || (Texto.Replace("_", "") == "")))
                    return true;

                if ((!PodeSerNulo) && ((Texto.Replace("_", "").Trim() == "") || (Texto.Replace("_", "").Trim() == null) || (Texto.Replace("_", "") == "")))
                    return false;

                if ((!PodeTerMenos) && ((Texto.Replace("_", "").Trim().Length < textbox.MaxLength)) || (Texto.Replace("_", "").Trim().Length < textbox.Mask.Length))
                    return false;

                else
                {
                    switch (Tipo)
                    {
                        case "CNPJ":
                            return (ValidaCNPJ(Texto));

                        case "CPF":
                            return (ValidaCPF(Texto));

                        case "Data":
                            return (ValidaDATA(Texto));

                        case "UF":
                            return (ValidaUF(Texto));

                        case "Numero":
                            return (ValidaNumero(Texto));

                        case "Valor":
                            return (ValidaValor(Texto));

                        case "E-mail":
                            return REGEX_MAIL.IsMatch(Texto);

                        case "IP":
                            return REGEX_IP.IsMatch(Texto);

                        case "MAC":
                            return REGEX_MAC.IsMatch(Texto);

                        case "CEP":
                            return REGEX_CEP.IsMatch(Texto);

                        case "Telefone":
                            return REGEX_TELEFONE.IsMatch(Texto);

                        case "Hora":
                            return REGEX_HORA.IsMatch(Texto);

                        case "Conta":
                            return REGEX_CONTA.IsMatch(Texto);

                        case "IPv6":
                            return REGEX_IP6.IsMatch(Texto);

                        default:
                            return true;
                    }
                }
            }
            catch // Houve algum problema nos módulos de validação (preenchimento incorreto, etc)
            {
                return false;
            }
        }
    }
}
