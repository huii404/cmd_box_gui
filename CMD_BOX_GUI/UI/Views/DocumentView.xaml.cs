using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CMD_BOX_GUI.Core;
using CMD_BOX_GUI.Models;
using CMD_BOX_GUI.Services;
using Microsoft.Win32;

namespace CMD_BOX_GUI.UI.Views
{
    public partial class DocumentView : UserControl
    {
        private readonly ObservableCollection<DocumentBatchItem> _ocrBatchItems = new();
        private readonly ObservableCollection<string> _pdfImages = new();
        private bool _isRunning;

        public DocumentView()
        {
            InitializeComponent();
            DgOcrFiles.ItemsSource = _ocrBatchItems;
            LstPdfImages.ItemsSource = _pdfImages;
        }

        private void DocumentView_Loaded(object sender, RoutedEventArgs e)
        {
            if (CmbOcrLanguage.Items.Count == 0)
            {
                var langs = DocumentService.Instance.GetAvailableOcrLanguages();
                CmbOcrLanguage.ItemsSource = langs;
                CmbOcrLanguage.DisplayMemberPath = "DisplayName";
                CmbOcrLanguage.SelectedValuePath = "Code";
                CmbOcrLanguage.SelectedIndex = 0;
            }
        }

        // =========================================================================
        // ĐIỀU HƯỚNG SUB-TABS TRÊN ĐỈNH
        // =========================================================================

        private void TabDoc_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb) return;

            PnlDocOcr.Visibility = rb == TabDocOcr ? Visibility.Visible : Visibility.Collapsed;
            PnlDocPdf.Visibility = rb == TabDocPdf ? Visibility.Visible : Visibility.Collapsed;
            PnlDocText.Visibility = rb == TabDocText ? Visibility.Visible : Visibility.Collapsed;

            TxtStatus.Text = rb == TabDocOcr ? "Chế độ OCR nhận diện văn bản từ hình ảnh."
                           : rb == TabDocPdf ? "Chế độ đóng gói tài liệu PDF & trích xuất tệp."
                           : "Chế độ chuyển đổi font chữ, làm sạch & tiện ích văn bản.";
        }

        // =========================================================================
        // 1. TÁC VỤ OCR ẢNH SANG TXT
        // =========================================================================

        private void BtnAddOcrImages_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn các tệp ảnh để nhận diện OCR",
                Filter = "Tệp Ảnh (*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.tiff)|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.tiff|Tất cả tệp (*.*)|*.*",
                Multiselect = true
            };

            if (dlg.ShowDialog() == true)
            {
                ImportImages(dlg.FileNames);
            }
        }

        public void ImportImages(string[] fileNames)
        {
            int added = 0;
            foreach (var file in fileNames)
            {
                if (File.Exists(file) && !_ocrBatchItems.Any(x => x.FilePath.Equals(file, StringComparison.OrdinalIgnoreCase)))
                {
                    var fi = new FileInfo(file);
                    _ocrBatchItems.Add(new DocumentBatchItem
                    {
                        FilePath = file,
                        FileSizeBytes = fi.Length,
                        DocType = "Ảnh OCR",
                        Status = "Sẵn sàng"
                    });
                    added++;
                }
            }

            TxtOcrCountBadge.Text = $"{_ocrBatchItems.Count} ảnh trong danh sách";
            if (added > 0)
            {
                Logger.Info($"[Document] Đã thêm {added} ảnh vào danh sách OCR.");
            }
        }

        private void PnlDocOcr_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                string[] validExts = { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tiff" };
                var imageFiles = files.Where(f => File.Exists(f) && validExts.Contains(Path.GetExtension(f).ToLowerInvariant())).ToArray();
                if (imageFiles.Length > 0)
                {
                    ImportImages(imageFiles);
                }
                e.Handled = true;
            }
        }

        private void BtnAddOcrFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog
            {
                Title = "Chọn thư mục chứa hình ảnh cần OCR"
            };

            if (dlg.ShowDialog() == true && Directory.Exists(dlg.FolderName))
            {
                string[] validExts = { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tiff" };
                var files = Directory.EnumerateFiles(dlg.FolderName, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => validExts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .ToArray();

                ImportImages(files);
            }
        }

        private void BtnClearOcrList_Click(object sender, RoutedEventArgs e)
        {
            _ocrBatchItems.Clear();
            TxtOcrCountBadge.Text = "0 ảnh trong danh sách";
            TxtOcrPreview.Clear();
            TxtEditorStats.Text = "0 từ | 0 ký tự";
            Logger.Info("[Document] Đã xóa danh sách ảnh OCR.");
        }

        private async void BtnStartOcrBatch_Click(object sender, RoutedEventArgs e)
        {
            if (_ocrBatchItems.Count == 0)
            {
                MessageBox.Show("Vui lòng thêm ít nhất một tệp ảnh để thực hiện OCR.", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_isRunning) return;
            SetRunning(true, "Đang nhận diện ký tự OCR...");

            string selectedLangCode = CmbOcrLanguage.SelectedValue as string ?? "Auto";
            bool exportTxt = ChkExportIndividualTxt.IsChecked == true;
            bool copyClipboard = ChkAutoCopyClipboard.IsChecked == true;

            PbDocTotal.Visibility = Visibility.Visible;
            PbDocTotal.Value = 0;
            PbDocTotal.Maximum = _ocrBatchItems.Count;

            var progress = new Progress<(int current, int total, string fileName)>(p =>
            {
                PbDocTotal.Value = p.current;
                TxtStatus.Text = $"Đang OCR ({p.current}/{p.total}): {p.fileName}";
            });

            var stopwatch = Stopwatch.StartNew();

            try
            {
                await DocumentService.Instance.BatchOcrImagesAsync(
                    _ocrBatchItems,
                    selectedLangCode,
                    outputFolder: null,
                    exportTxtFiles: exportTxt,
                    progress: progress
                );

                stopwatch.Stop();
                Logger.Success($"[OCR] Hoàn tất nhận diện {_ocrBatchItems.Count} ảnh trong {stopwatch.ElapsedMilliseconds} ms!");

                // Hiển thị kết quả của mục đầu tiên lên editor
                var first = _ocrBatchItems.FirstOrDefault(x => !string.IsNullOrEmpty(x.ResultText));
                if (first != null)
                {
                    DgOcrFiles.SelectedItem = first;
                    TxtOcrPreview.Text = first.ResultText;
                }

                if (copyClipboard)
                {
                    string consolidated = string.Join(Environment.NewLine + Environment.NewLine,
                        _ocrBatchItems.Where(x => !string.IsNullOrWhiteSpace(x.ResultText)).Select(x => x.ResultText));
                    if (!string.IsNullOrWhiteSpace(consolidated))
                    {
                        Clipboard.SetText(consolidated);
                        Logger.Info("[OCR] Đã tự động sao chép toàn bộ kết quả vào Clipboard!");
                    }
                }

                TxtStatus.Text = $"Hoàn tất OCR {_ocrBatchItems.Count} ảnh ({stopwatch.Elapsed.TotalSeconds:0.1}s).";
            }
            catch (Exception ex)
            {
                Logger.Error($"[OCR] Thất bại: {ex.Message}");
                MessageBox.Show($"Lỗi OCR: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                PbDocTotal.Visibility = Visibility.Collapsed;
                SetRunning(false, "Sẵn sàng.");
            }
        }

        private void DgOcrFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgOcrFiles.SelectedItem is DocumentBatchItem item)
            {
                TxtOcrPreview.Text = item.ResultText;
            }
        }

        private void TxtOcrPreview_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = TxtOcrPreview.Text;
            int chars = text.Length;
            int words = string.IsNullOrWhiteSpace(text) ? 0 : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            TxtEditorStats.Text = $"{words} từ | {chars} ký tự";

            if (DgOcrFiles.SelectedItem is DocumentBatchItem item && item.ResultText != text)
            {
                item.ResultText = text;
            }
        }

        private void BtnCopyOcrPreview_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtOcrPreview.Text))
            {
                Clipboard.SetText(TxtOcrPreview.Text);
                Logger.Info("[Document] Đã sao chép nội dung vào Clipboard.");
                TxtStatus.Text = "Đã sao chép nội dung vào Clipboard!";
            }
        }

        private async void BtnSaveOcrPreview_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtOcrPreview.Text))
            {
                MessageBox.Show("Nội dung xem trước trống.", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Title = "Lưu nội dung OCR ra tệp văn bản",
                Filter = "Text Document (*.txt)|*.txt|All Files (*.*)|*.*",
                FileName = "ocr_result.txt"
            };

            if (dlg.ShowDialog() == true)
            {
                await File.WriteAllTextAsync(dlg.FileName, TxtOcrPreview.Text, System.Text.Encoding.UTF8);
                Logger.Success($"[Document] Đã lưu tệp: {dlg.FileName}");
                TxtStatus.Text = $"Đã lưu tệp: {Path.GetFileName(dlg.FileName)}";
            }
        }

        private void BtnCleanOcrPreview_Click(object sender, RoutedEventArgs e)
        {
            TxtOcrPreview.Text = DocumentService.Instance.CleanTextData(
                TxtOcrPreview.Text,
                removeBlankLines: true,
                trimLines: true,
                deduplicate: false,
                sortLines: false,
                sortDescending: false
            );
            TxtStatus.Text = "Đã thu gọn khoảng trắng và xóa các dòng trống.";
        }

        private void BtnClearOcrPreview_Click(object sender, RoutedEventArgs e)
        {
            TxtOcrPreview.Clear();
            TxtStatus.Text = "Đã xóa trắng khung xem trước.";
        }

        private void BtnOpenTxtResult_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DocumentBatchItem item)
            {
                if (File.Exists(item.OutputPath))
                {
                    Process.Start(new ProcessStartInfo { FileName = item.OutputPath, UseShellExecute = true });
                }
                else if (!string.IsNullOrWhiteSpace(item.ResultText))
                {
                    TxtOcrPreview.Text = item.ResultText;
                    DgOcrFiles.SelectedItem = item;
                    TxtStatus.Text = $"Đang hiển thị kết quả của {item.FileName}";
                }
                else
                {
                    MessageBox.Show("Tệp này chưa được chạy OCR.", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void BtnRemoveOcrItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DocumentBatchItem item)
            {
                _ocrBatchItems.Remove(item);
                TxtOcrCountBadge.Text = $"{_ocrBatchItems.Count} ảnh trong danh sách";
            }
        }

        // =========================================================================
        // 2. SUB-TAB 2: ĐÓNG GÓI PDF & TRÍCH XUẤT TỆP
        // =========================================================================

        private void BtnAddPdfImages_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn các ảnh để đóng gói vào tệp PDF",
                Filter = "Tệp Ảnh (*.png;*.jpg;*.jpeg;*.webp;*.bmp)|*.png;*.jpg;*.jpeg;*.webp;*.bmp|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (dlg.ShowDialog() == true)
            {
                foreach (var f in dlg.FileNames)
                {
                    if (!_pdfImages.Contains(f)) _pdfImages.Add(f);
                }
                TxtPdfImageCount.Text = $"{_pdfImages.Count} ảnh đã chọn";
            }
        }

        private void LstPdfImages_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                string[] validExts = { ".png", ".jpg", ".jpeg", ".webp", ".bmp" };
                foreach (var f in files)
                {
                    if (File.Exists(f) && validExts.Contains(Path.GetExtension(f).ToLowerInvariant()) && !_pdfImages.Contains(f))
                    {
                        _pdfImages.Add(f);
                    }
                }
                TxtPdfImageCount.Text = $"{_pdfImages.Count} ảnh đã chọn";
                e.Handled = true;
            }
        }

        private void BtnClearPdfImages_Click(object sender, RoutedEventArgs e)
        {
            _pdfImages.Clear();
            TxtPdfImageCount.Text = "0 ảnh đã chọn";
        }

        private void BtnMovePdfImageUp_Click(object sender, RoutedEventArgs e)
        {
            int idx = LstPdfImages.SelectedIndex;
            if (idx > 0)
            {
                string item = _pdfImages[idx];
                _pdfImages.RemoveAt(idx);
                _pdfImages.Insert(idx - 1, item);
                LstPdfImages.SelectedIndex = idx - 1;
            }
        }

        private void BtnMovePdfImageDown_Click(object sender, RoutedEventArgs e)
        {
            int idx = LstPdfImages.SelectedIndex;
            if (idx >= 0 && idx < _pdfImages.Count - 1)
            {
                string item = _pdfImages[idx];
                _pdfImages.RemoveAt(idx);
                _pdfImages.Insert(idx + 1, item);
                LstPdfImages.SelectedIndex = idx + 1;
            }
        }

        private async void BtnExportImagesToPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_pdfImages.Count == 0)
            {
                MessageBox.Show("Vui lòng thêm ít nhất một ảnh để đóng gói thành PDF.", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Title = "Lưu file PDF hợp nhất",
                Filter = "PDF Document (*.pdf)|*.pdf",
                FileName = $"Document_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
            };

            if (dlg.ShowDialog() == true)
            {
                SetRunning(true, "Đang đóng gói PDF...");
                try
                {
                    bool ok = await DocumentService.Instance.ConvertImagesToPdfAsync(_pdfImages, dlg.FileName);
                    if (ok)
                    {
                        Logger.Success($"[PDF] Đã tạo thành công tệp PDF: {dlg.FileName}");
                        TxtStatus.Text = $"Đã tạo thành công: {Path.GetFileName(dlg.FileName)}";

                        if (MessageBox.Show("Đã đóng gói PDF thành công! Bạn có muốn mở tệp ngay không?", "Thành Công", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                        {
                            Process.Start(new ProcessStartInfo { FileName = dlg.FileName, UseShellExecute = true });
                        }
                    }
                    else
                    {
                        MessageBox.Show("Tạo PDF thất bại. Vui lòng kiểm tra lại định dạng ảnh.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"[PDF] Lỗi: {ex.Message}");
                    MessageBox.Show($"Lỗi tạo PDF: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    SetRunning(false, "Sẵn sàng.");
                }
            }
        }

        private async void BtnExtractDocxToEditor_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn tệp Word DOCX để trích xuất văn bản",
                Filter = "Word Document (*.docx)|*.docx"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string text = await DocumentService.Instance.ExtractTextFromDocxAsync(dlg.FileName);
                    TxtToolWorkspace.Text = text;
                    TabDocText.IsChecked = true;
                    TabDoc_Click(TabDocText, new RoutedEventArgs());
                    Logger.Success($"[DOCX] Đã đọc xong văn bản từ {Path.GetFileName(dlg.FileName)} vào Editor.");
                }
                catch (Exception ex)
                {
                    Logger.Error($"[DOCX] Lỗi: {ex.Message}");
                    MessageBox.Show($"Lỗi trích xuất DOCX: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnExtractDocxToTxt_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn tệp Word DOCX để trích xuất ra .txt",
                Filter = "Word Document (*.docx)|*.docx"
            };

            if (dlg.ShowDialog() == true)
            {
                var saveDlg = new SaveFileDialog
                {
                    Title = "Lưu ra tệp văn bản",
                    Filter = "Text Document (*.txt)|*.txt",
                    FileName = Path.GetFileNameWithoutExtension(dlg.FileName) + ".txt"
                };

                if (saveDlg.ShowDialog() == true)
                {
                    try
                    {
                        string text = await DocumentService.Instance.ExtractTextFromDocxAsync(dlg.FileName);
                        await File.WriteAllTextAsync(saveDlg.FileName, text, System.Text.Encoding.UTF8);
                        Logger.Success($"[DOCX] Đã xuất file TXT thành công: {saveDlg.FileName}");
                        TxtStatus.Text = $"Đã xuất: {Path.GetFileName(saveDlg.FileName)}";
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"[DOCX] Lỗi: {ex.Message}");
                        MessageBox.Show($"Lỗi xuất TXT: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void BtnPackAiContext_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn các tệp mã nguồn / tài liệu để đóng gói cho AI Prompt",
                Filter = "Source & Documents (*.cs;*.xaml;*.json;*.txt;*.py;*.md;*.docx)|*.cs;*.xaml;*.json;*.txt;*.py;*.md;*.docx|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (dlg.ShowDialog() == true && dlg.FileNames.Length > 0)
            {
                string packed = await DocumentService.Instance.ConsolidateFilesForAiPromptAsync(dlg.FileNames);
                TxtToolWorkspace.Text = packed;
                TabDocText.IsChecked = true;
                TabDoc_Click(TabDocText, new RoutedEventArgs());
                Logger.Success($"[AI Context] Đã đóng gói {dlg.FileNames.Length} tệp vào Không Gian Làm Việc.");
            }
        }

        private async void BtnPackAiContextToFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn các tệp để đóng gói ra file Context.txt",
                Filter = "Source & Documents (*.cs;*.xaml;*.json;*.txt;*.py;*.md;*.docx)|*.cs;*.xaml;*.json;*.txt;*.py;*.md;*.docx|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (dlg.ShowDialog() == true && dlg.FileNames.Length > 0)
            {
                var saveDlg = new SaveFileDialog
                {
                    Title = "Lưu tệp Context đóng gói",
                    Filter = "Text Document (*.txt)|*.txt",
                    FileName = $"AI_Context_Dump_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (saveDlg.ShowDialog() == true)
                {
                    string packed = await DocumentService.Instance.ConsolidateFilesForAiPromptAsync(dlg.FileNames);
                    await File.WriteAllTextAsync(saveDlg.FileName, packed, System.Text.Encoding.UTF8);
                    Logger.Success($"[AI Context] Đã xuất file: {saveDlg.FileName}");
                    TxtStatus.Text = $"Đã xuất file Context ({dlg.FileNames.Length} tệp)";
                }
            }
        }

        // =========================================================================
        // 3. SUB-TAB 3: CHUYỂN ĐỔI & TIỆN ÍCH VĂN BẢN
        // =========================================================================

        private void TxtToolWorkspace_TextChanged(object sender, TextChangedEventArgs e)
        {
            string t = TxtToolWorkspace.Text;
            int lines = t.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Length;
            int words = string.IsNullOrWhiteSpace(t) ? 0 : t.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            int chars = t.Length;
            TxtToolStats.Text = $"{lines} dòng | {words} từ | {chars} ký tự";
        }

        private async void BtnToolLoadFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Mở tệp văn bản",
                Filter = "Văn bản & Mã nguồn (*.txt;*.log;*.json;*.md;*.csv;*.cs;*.xaml;*.xml)|*.txt;*.log;*.json;*.md;*.csv;*.cs;*.xaml;*.xml|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                TxtToolWorkspace.Text = await File.ReadAllTextAsync(dlg.FileName);
                TxtStatus.Text = $"Đã nạp tệp: {Path.GetFileName(dlg.FileName)}";
            }
        }

        private async void BtnToolSaveFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Lưu tệp văn bản",
                Filter = "Text Document (*.txt)|*.txt|All Files (*.*)|*.*",
                FileName = "document.txt"
            };

            if (dlg.ShowDialog() == true)
            {
                await File.WriteAllTextAsync(dlg.FileName, TxtToolWorkspace.Text, System.Text.Encoding.UTF8);
                Logger.Success($"[Document] Đã lưu tệp: {dlg.FileName}");
                TxtStatus.Text = $"Đã lưu: {Path.GetFileName(dlg.FileName)}";
            }
        }

        private void BtnToolCopy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtToolWorkspace.Text))
            {
                Clipboard.SetText(TxtToolWorkspace.Text);
                TxtStatus.Text = "Đã sao chép toàn bộ văn bản vào Clipboard!";
            }
        }

        private void BtnToolClear_Click(object sender, RoutedEventArgs e)
        {
            TxtToolWorkspace.Clear();
            TxtStatus.Text = "Đã xóa trắng khung soạn thảo.";
        }

        private void BtnFixTcvn3_Click(object sender, RoutedEventArgs e)
        {
            TxtToolWorkspace.Text = DocumentService.Instance.ConvertTcvn3ToUnicode(TxtToolWorkspace.Text);
            TxtStatus.Text = "Đã chuyển đổi mã TCVN3 (ABC) sang Unicode chuẩn!";
            Logger.Success("[Font] Đã chuyển đổi bảng mã TCVN3 sang Unicode.");
        }

        private void BtnFixVni_Click(object sender, RoutedEventArgs e)
        {
            TxtToolWorkspace.Text = DocumentService.Instance.ConvertVniToUnicode(TxtToolWorkspace.Text);
            TxtStatus.Text = "Đã chuyển đổi mã VNI-Windows sang Unicode chuẩn!";
            Logger.Success("[Font] Đã chuyển đổi bảng mã VNI sang Unicode.");
        }

        private void BtnStripAccents_Click(object sender, RoutedEventArgs e)
        {
            TxtToolWorkspace.Text = DocumentService.Instance.RemoveVietnameseAccents(TxtToolWorkspace.Text);
            TxtStatus.Text = "Đã loại bỏ toàn bộ dấu Tiếng Việt!";
        }

        private void BtnCaseConvert_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string mode)
            {
                TxtToolWorkspace.Text = DocumentService.Instance.ConvertCase(TxtToolWorkspace.Text, mode);
                TxtStatus.Text = $"Đã đổi kiểu chữ sang {mode}!";
            }
        }

        private void BtnApplyCleaning_Click(object sender, RoutedEventArgs e)
        {
            bool remBlank = ChkCleanEmptyLines.IsChecked == true;
            bool trim = ChkCleanTrim.IsChecked == true;
            bool dedup = ChkCleanDeduplicate.IsChecked == true;
            bool sort = ChkCleanSort.IsChecked == true;

            TxtToolWorkspace.Text = DocumentService.Instance.CleanTextData(
                TxtToolWorkspace.Text,
                removeBlankLines: remBlank,
                trimLines: trim,
                deduplicate: dedup,
                sortLines: sort,
                sortDescending: false
            );
            TxtStatus.Text = "Đã làm sạch dữ liệu văn bản theo cấu hình!";
        }

        private void BtnExtractEmails_Click(object sender, RoutedEventArgs e)
        {
            var emails = DocumentService.Instance.ExtractEmails(TxtToolWorkspace.Text);
            TxtToolWorkspace.Text = string.Join(Environment.NewLine, emails);
            TxtStatus.Text = $"Đã trích xuất {emails.Count} địa chỉ email duy nhất.";
        }

        private void BtnExtractUrls_Click(object sender, RoutedEventArgs e)
        {
            var urls = DocumentService.Instance.ExtractUrls(TxtToolWorkspace.Text);
            TxtToolWorkspace.Text = string.Join(Environment.NewLine, urls);
            TxtStatus.Text = $"Đã trích xuất {urls.Count} đường link URLs duy nhất.";
        }

        private void BtnExtractPhones_Click(object sender, RoutedEventArgs e)
        {
            var phones = DocumentService.Instance.ExtractPhoneNumbers(TxtToolWorkspace.Text);
            TxtToolWorkspace.Text = string.Join(Environment.NewLine, phones);
            TxtStatus.Text = $"Đã trích xuất {phones.Count} số điện thoại duy nhất.";
        }

        private void BtnExtractIps_Click(object sender, RoutedEventArgs e)
        {
            var ips = DocumentService.Instance.ExtractIpAddresses(TxtToolWorkspace.Text);
            TxtToolWorkspace.Text = string.Join(Environment.NewLine, ips);
            TxtStatus.Text = $"Đã trích xuất {ips.Count} địa chỉ IP duy nhất.";
        }

        private void BtnBase64Encode_Click(object sender, RoutedEventArgs e)
        {
            TxtToolWorkspace.Text = DocumentService.Instance.Base64EncodeText(TxtToolWorkspace.Text);
            TxtStatus.Text = "Đã mã hóa Base64 thành công!";
        }

        private void BtnBase64Decode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TxtToolWorkspace.Text = DocumentService.Instance.Base64DecodeText(TxtToolWorkspace.Text);
                TxtStatus.Text = "Đã giải mã Base64 thành công!";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chuỗi không phải Base64 hợp lệ: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnUrlEncode_Click(object sender, RoutedEventArgs e)
        {
            TxtToolWorkspace.Text = DocumentService.Instance.UrlEncode(TxtToolWorkspace.Text);
            TxtStatus.Text = "Đã URL Encode thành công!";
        }

        private void BtnUrlDecode_Click(object sender, RoutedEventArgs e)
        {
            TxtToolWorkspace.Text = DocumentService.Instance.UrlDecode(TxtToolWorkspace.Text);
            TxtStatus.Text = "Đã URL Decode thành công!";
        }

        private async void BtnComputeFileHash_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn tệp để tính mã băm kiểm tra tính toàn vẹn",
                Filter = "Tất cả tệp (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                string path = dlg.FileName;
                string md5 = await DocumentService.Instance.ComputeFileChecksumAsync(path, "MD5");
                string sha256 = await DocumentService.Instance.ComputeFileChecksumAsync(path, "SHA256");

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"TỆP: {Path.GetFileName(path)}");
                sb.AppendLine($"ĐƯỜNG DẪN: {path}");
                sb.AppendLine($"KÍCH THƯỚC: {new FileInfo(path).Length:N0} bytes");
                sb.AppendLine($"MD5:    {md5}");
                sb.AppendLine($"SHA256: {sha256}");

                TxtToolWorkspace.Text = sb.ToString();
                TxtStatus.Text = $"Đã tính toán mã băm cho {Path.GetFileName(path)}";
            }
        }

        private void SetRunning(bool isRunning, string status)
        {
            _isRunning = isRunning;
            TxtStatus.Text = status;
            BtnStartOcrBatch.IsEnabled = !isRunning;
            BtnAddOcrImages.IsEnabled = !isRunning;
            BtnAddOcrFolder.IsEnabled = !isRunning;
            BtnClearOcrList.IsEnabled = !isRunning;
        }
    }
}
