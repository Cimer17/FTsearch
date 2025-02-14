using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

    public NestedListBuilder(TS4App intermechAPI)
    {
        _api = intermechAPI;
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
    public Item BuildNestedList(int artId, string inputDesignation)
    {
        string rootDesignation = inputDesignation; 
        Console.WriteLine($"Корневой элемент: {rootDesignation}");
        Item rootItem = new Item(rootDesignation, "", "1");
        Console.WriteLine($"Открываем структуру для ArtID: {artId}");
        _api.OpenArticleStructure(artId);
        BuildSubItems(rootItem);
        _api.CloseArticleStructure();
        return rootItem;
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
            ChildRow row = new ChildRow
            {
                ArtID = _api.asGetArtID(),
                Designation = _api.asGetArtDesignation(),
                Name = _api.asGetArtName(),
                Count = _api.asGetArtCountText()
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
    /// Рекурсивно выводит дерево состава в консоль с отступами
    /// </summary>
    public void PrintNestedList(Item item, int indent = 0)
    {
        Console.WriteLine($"{new string(' ', indent)}{item.Designation} - {item.Name} (Количество: {item.Count})");
        foreach (var subItem in item.SubItems)
        {
            PrintNestedList(subItem, indent + 2);
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

class Program
{
    static TS4App S4App = new TS4App();

    public static void Main(string[] args)
    {
        Console.WriteLine("Начало выполнения программы.");
        NestedListBuilder builder = new NestedListBuilder(S4App);
        builder.ConnectSearch();

        //Console.Write("Введите обозначение изделия: ");
        //string designation = Console.ReadLine();
        //Console.WriteLine($"Поиск изделия по обозначению: {designation}");
        //int artID = S4App.GetArtID_ByDesignation(designation);
        //if (artID <= 0)
        //{
        //    Console.WriteLine("Изделие не найдено по обозначению. Пробуем через документ...");
        //    int docID = S4App.GetDocID_ByDesignation(designation);
        //    if (docID > 0)
        //    {
        //        artID = S4App.GetArtID_ByDocID(docID);
        //        Console.WriteLine($"Нашли ArtID через документ: {artID}");
        //    }
        //    else
        //    {
        //        Console.WriteLine("Не удалось найти документ.");
        //        return;
        //    }
        //}
        //else
        //{
        //    Console.WriteLine($"Найден ArtID: {artID}");
        //}


        // Для search (работа с выделенным обьектом)
        var selecet = S4App.GetSelectedItems();
        selecet.FirstSelected(); 
        int artID = S4App.ActiveArtID;
        S4App.OpenArticle(artID);
        string designation = S4App.GetArticleDesignation();
        S4App.CloseArticle();



        Console.WriteLine("Построение дерева состава...");
        // Передаём исходное обозначение для корневого узла, если специальные методы не доступны
        Item rootItem = builder.BuildNestedList(artID, designation);
        Console.WriteLine("Дерево состава построено. Вывод в консоль:");
        builder.PrintNestedList(rootItem);

        Console.WriteLine("Генерация HTML-вывода...");
        string html = builder.GenerateHtml(rootItem);
        File.WriteAllText("structure.html", html);
        Console.WriteLine("HTML-файл 'structure.html' создан.");

        Console.WriteLine("Программа завершена. Для закрытия нажмите любую клавишу...");
        Console.ReadLine();
    }
}