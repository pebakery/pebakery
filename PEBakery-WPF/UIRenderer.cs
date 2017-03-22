using PEBakery.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PEBakery.WPF
{
    public class UIRenderer
    {
        private Canvas canvas;
        private Plugin plugin;
        private List<UICommand> uiCodes;

        public UIRenderer(Canvas canvas, Plugin plugin)
        {
            this.canvas = canvas;
            this.plugin = plugin;
            if (plugin.Sections.ContainsKey("Interface"))
                this.uiCodes = plugin.Sections["Interface"].GetUICodes(true);
            else
                this.uiCodes = null;
        }

        public void Render()
        {
            if (uiCodes == null) // This plugin does not have 'Interface' section
                return; 

            foreach (UICommand uiCmd in uiCodes)
            {
                if (uiCmd.Visibility == false)
                    continue;

                switch (uiCmd.Type)
                {
                    case UIControlType.TextBox:
                        break;
                    case UIControlType.TextLabel:
                        {
                            UIInfo_TextLabel info = uiCmd.Info as UIInfo_TextLabel;
                            if (info == null || info.Valid == false)
                                continue;
                            TextBlock block = new TextBlock();
                            block.Text = uiCmd.Text;
                            block.TextWrapping = TextWrapping.Wrap;
                            // TODO: FontSize and Coord from WinBuilder does not match with WPF Canvas! Need adjust...
                            block.FontSize = info.FontSize * 1.2;
                            switch (info.Style)
                            {
                                case UIInfo_TextLabel_Style.Bold:
                                    block.FontWeight = FontWeights.Bold;
                                    break;
                                case UIInfo_TextLabel_Style.Italic:
                                    block.FontStyle = FontStyles.Italic;
                                    break;
                                case UIInfo_TextLabel_Style.Underline:
                                    block.TextDecorations = TextDecorations.Underline;
                                    break;
                                case UIInfo_TextLabel_Style.Strike:
                                    block.TextDecorations = TextDecorations.Strikethrough;
                                    break;
                            }
                            Canvas.SetLeft(block, uiCmd.Rect.Left);
                            Canvas.SetTop(block, uiCmd.Rect.Top);
                            block.Width = uiCmd.Rect.Width;
                            block.Height = uiCmd.Rect.Height;
                            canvas.Children.Add(block);
                        }
                        break;
                    case UIControlType.NumberBox:
                        break;
                    case UIControlType.CheckBox:
                        break;
                    case UIControlType.ComboBox:
                        break;
                    case UIControlType.Image:
                        break;
                    case UIControlType.TextFile:
                        break;
                    case UIControlType.Button:
                        break;
                    case UIControlType.CheckList:
                        break;
                    case UIControlType.WebLabel:
                        break;
                    case UIControlType.RadioButton:
                        break;
                    case UIControlType.Bevel:
                        break;
                    case UIControlType.FileBox:
                        break;
                    case UIControlType.RadioGroup:
                        break;
                    default:
                        break;
                }
            }

            return;
        }

        public void RenderTextBox(List<string> operands)
        {

        }

    }   
}
