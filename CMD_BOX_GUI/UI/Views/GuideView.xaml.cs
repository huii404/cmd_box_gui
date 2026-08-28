using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace CMD_BOX_GUI.UI.Views
{
    public partial class GuideView : UserControl
    {
        private readonly List<Expander> _expanders = new();
        private string _selectedCategory = "ALL";

        public GuideView()
        {
            InitializeComponent();

            _expanders.Add(CardOptimizer);
            _expanders.Add(CardNetwork);
            _expanders.Add(CardUtilities);
            _expanders.Add(CardMedia);
            _expanders.Add(CardHotkeys);
        }

        private void FilterChip_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string tag)
            {
                _selectedCategory = tag.ToUpperInvariant();
                ApplyFilter();
            }
        }

        private void TxtSearchGuide_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (BtnClearSearch != null)
            {
                BtnClearSearch.Visibility = string.IsNullOrWhiteSpace(TxtSearchGuide.Text) 
                    ? Visibility.Collapsed 
                    : Visibility.Visible;
            }
            ApplyFilter();
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtSearchGuide.Text = string.Empty;
        }

        private void BtnExpandAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var exp in _expanders)
            {
                if (exp.Visibility == Visibility.Visible)
                {
                    exp.IsExpanded = true;
                }
            }
        }

        private void BtnCollapseAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var exp in _expanders)
            {
                exp.IsExpanded = false;
            }
        }

        private void ApplyFilter()
        {
            if (_expanders.Count == 0) return;

            string query = RemoveDiacritics(TxtSearchGuide?.Text?.Trim() ?? string.Empty).ToLowerInvariant();
            int visibleCount = 0;

            foreach (var exp in _expanders)
            {
                string tag = exp.Tag?.ToString() ?? string.Empty;
                string tagNormalized = RemoveDiacritics(tag).ToLowerInvariant();

                // 1. Kiểm tra Category
                bool matchCategory = _selectedCategory == "ALL" || tag.Contains(_selectedCategory, StringComparison.OrdinalIgnoreCase);

                // 2. Kiểm tra Search Text
                bool matchSearch = string.IsNullOrEmpty(query) || tagNormalized.Contains(query);

                if (matchCategory && matchSearch)
                {
                    exp.Visibility = Visibility.Visible;
                    if (!string.IsNullOrEmpty(query))
                    {
                        exp.IsExpanded = true; // Tự động mở rộng khi người dùng tìm kiếm thấy kết quả
                    }
                    visibleCount++;
                }
                else
                {
                    exp.Visibility = Visibility.Collapsed;
                }
            }

            if (PnlNoResults != null)
            {
                PnlNoResults.Visibility = visibleCount == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            string normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (char c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
        }
    }
}
