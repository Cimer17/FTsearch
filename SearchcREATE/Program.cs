using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;
using S4;

/// <summary>
/// Класс изделия – узел дерева состава
/// </summary>
public class Item
{
    public string Designation { get; set; }
    public string Name { get; set; }
    public string Count { get; set; }
    public List<Item> SubItems { get; set; }

    public Item(string designation, string name, string count)
    {
        Designation = designation;
        Name = name;
        Count = count;
        SubItems = new List<Item>();
    }
}

/// <summary>
/// Вспомогательный класс для буферизации строки структуры
/// </summary>
public class ChildRow
{
    public int ArtID { get; set; }
    public string Designation { get; set; }
    public string Name { get; set; }
    public string Count { get; set; }
}

/// <summary>
/// Класс для построения дерева состава изделия и генерации HTML-вывода
/// </summary>
public class NestedListBuilder
{
    private TS4App _api;
    private int _uniqueIdCounter = 0; // Глобальный счетчик для уникальных id
    private bool _switchdocumentation; // Переключатель, показывать документацию или нет

    public NestedListBuilder(TS4App intermechAPI, bool switchdocumentation)
    {
        _api = intermechAPI;
        _switchdocumentation = switchdocumentation;
    }

    /// <summary>
    /// Устанавливает соединение с SEArch
    /// </summary>
    public bool ConnectSearch()
    {
        Console.WriteLine("Подключение к SEArch...");
        while (_api.Login() != 1)
        {
            Console.WriteLine("Ожидание подключения...");
            System.Threading.Thread.Sleep(1000);
        }
        Console.WriteLine("Подключение установлено.");
        return true;
    }

    /// <summary>
    /// Строит дерево состава изделия по заданному ArtID.
    /// Для корневого элемента получаем базовые данные отдельно.
    /// </summary>
    public Item BuildNestedList(int artId, string inputDesignation, string name)
    {
        Console.WriteLine($"Корневой элемент: {inputDesignation}");
        Item rootItem = new Item(inputDesignation, name, "1");
        Console.WriteLine($"Открываем структуру для ArtID: {artId}");
        
        _api.OpenArticleStructure(artId);
        BuildSubItems(rootItem);
        _api.CloseArticleStructure();
        
        return rootItem;
    }

    /// <summary>
    /// Метод проверки принадлежности Обьекта (СП, ТУ и т. п.)
    /// Если обьект принадлежит => true
    /// </summary>
    private bool CheckDesignator(string name) {
        if (name is null) {
            return false;
        }
        string[] names = name.Split();
        if (names.Length > 1) {
            switch (names[1])
            {
                case "ТУ":
                case "Э3":
                case "ГЧ":
                case "ТО":
                case "МЭ":
                case "СБ":
                case "ЗИ":
                case "ЗИ1":
                case "ТО-ЛУ":
                case "ПС-ЛУ":
                case "ЗИ1-ЛУ":
                case "ЗИ-ЛУ":
                case "ПС":
                case "ПЭЗ":
                case "ВС":
                    return true;
                default:
                    return false;
            }
        }
        return false;
    }


    /// <summary>
    /// Получение примечания, в случае если ко-во равно 0
    /// В далнящем дополнительная логика, метод дорабатывается
    /// </summary>
    /// <returns></returns>
    private string _GetRemark()
    {
        return _api.asGetArtRemark();
    }
    
    /// <summary>
    /// Рекурсивно обходит текущую открытую структуру.
    /// Сначала буферизуем все строки, затем для каждого открываем его структуру.
    /// </summary>
    private void BuildSubItems(Item parentItem)
    {
        List<ChildRow> rows = new List<ChildRow>();
        Console.WriteLine("Буферизуем строки текущей структуры...");
        _api.asFirst(); // Устанавливаем указатель на первую строку
        while (_api.asEof() == 0)
        {
            string count = _api.asGetArtCountText();
            string designation = _api.asGetArtDesignation();
            string remark = "";
            if (!_switchdocumentation) {
                if (CheckDesignator(designation))
                {
                    Console.WriteLine("Похоже на документацию, исключаю из дерева...");
                    _api.asNext();
                    continue;
                }
            }

            if (count.Split()[0] == "0")
            {
                remark = _GetRemark();
            }
            
            ChildRow row = new ChildRow
            {
                ArtID = _api.asGetArtID(),
                Designation = designation,
                Name = _api.asGetArtName(),
                Count = count,
            };
            Console.WriteLine($"Буферизуем строку: {row.Designation} - {row.Name} (ID: {row.ArtID}, Кол-во: {row.Count})");
            rows.Add(row);
            _api.asNext();
        }

        // Обрабатываем сохранённые строки
        foreach (var row in rows)
        {
            Item subItem = new Item(row.Designation, row.Name, row.Count);
            parentItem.SubItems.Add(subItem);
            Console.WriteLine($"Обрабатываем дочерний элемент: {row.Designation} - {row.Name} (ID: {row.ArtID})");
            if (row.ArtID > 0)
            {
                _api.OpenArticleStructure(row.ArtID);
                BuildSubItems(subItem);
                _api.CloseArticleStructure();
            }
        }
    }

    /// <summary>
    /// Генерирует HTML-код с вложенным списком, который можно раскрывать/сворачивать
    /// </summary>
    public string GenerateHtml(Item rootItem)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang='ru'>");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset='UTF-8'>");
        sb.AppendLine("  <title>Структура изделия</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    ul { list-style-type: none; padding-left: 20px; }");
        sb.AppendLine("    li { margin: 5px; }");
        sb.AppendLine("    .toggle { cursor: pointer; color: blue; font-weight: bold; margin-right: 5px; }");
        sb.AppendLine("    .hidden { display: none; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("  <script>");
        sb.AppendLine("    function toggle(id, btnId) {");
        sb.AppendLine("      var elem = document.getElementById(id);");
        sb.AppendLine("      var btn = document.getElementById(btnId);");
        sb.AppendLine("      if (elem.style.display === 'none') {");
        sb.AppendLine("        elem.style.display = 'block';");
        sb.AppendLine("        btn.innerHTML = '−';");
        sb.AppendLine("      } else {");
        sb.AppendLine("        elem.style.display = 'none';");
        sb.AppendLine("        btn.innerHTML = '+';");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine("  </script>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <h1>Структура изделия</h1>");
        sb.AppendLine(GenerateHtmlList(rootItem));
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    /// <summary>
    /// Рекурсивно строит HTML для вложенного списка, используя глобальный счетчик для уникальных id.
    /// </summary>
    private string GenerateHtmlList(Item item)
    {
        string currentId = "node" + (_uniqueIdCounter++);
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<ul>");
        sb.AppendLine("  <li>");
        // Если у элемента есть дочерние узлы, добавляем кнопку toggle
        if (item.SubItems.Count > 0)
        {
            sb.AppendLine($"    <span id='btn{currentId}' class='toggle' onclick=\"toggle('{currentId}', 'btn{currentId}')\">+</span>");
        }
        else
        {
            sb.AppendLine("    <span style='display:inline-block; width:16px;'></span>");
        }
        sb.AppendLine($"    {item.Designation} - {item.Name} (Количество: {item.Count})");
        // Если есть подузлы, строим их HTML и помещаем в контейнер с уникальным id
        if (item.SubItems.Count > 0)
        {
            sb.AppendLine($"    <div id='{currentId}' class='hidden'>");
            foreach (var subItem in item.SubItems)
            {
                sb.AppendLine(GenerateHtmlList(subItem));
            }
            sb.AppendLine("    </div>");
        }
        sb.AppendLine("  </li>");
        sb.AppendLine("</ul>");
        return sb.ToString();
    }
}

/// <summary>
/// Основной класс программы
/// </summary>
class Program
{
    static TS4App S4App = new TS4App();
    static bool _switchdocumentation;

    /// <summary>
    /// Точка запуска
    /// </summary>
    public static void Main(string[] args)
    {
        Console.WriteLine("Начало выполнения программы.");
        Console.WriteLine("Включать документацию (ТУ, СБ, ГЧ и т.п.)? д - да, н - нет :");
        string settings = Console.ReadLine().ToLower();
        _switchdocumentation = settings != "н";
        
        NestedListBuilder builder = new NestedListBuilder(S4App, _switchdocumentation);
        builder.ConnectSearch();
        
        Console.WriteLine("Считывания информации об изделии...");
        var selecet = S4App.GetSelectedItems();
        selecet.FirstSelected(); 
        int artID = S4App.ActiveArtID;
        S4App.OpenArticle(artID);
        string designation = S4App.GetArticleDesignation();
        string nameart = S4App.GetArticleName();
        S4App.CloseArticle();

        Console.WriteLine("Построение дерева состава...");
        Item rootItem = builder.BuildNestedList(artID, designation, nameart);

        Console.WriteLine("Генерация HTML-вывода...");
        string html = builder.GenerateHtml(rootItem);

        string path = @"C:\HtmlOr";
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        string filePath = Path.Combine(path, "structure.html");
        File.WriteAllText(filePath, html);
        Process.Start(filePath);
        Console.WriteLine("HTML-файл 'structure.html' создан!");

        Console.WriteLine("Программа завершена. Для закрытия нажмите любую клавишу...");
        Console.ReadLine();
    }
}