using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace HToolBase
{
    /// <summary>Products.dat 的存储结构：产品列表 + 上次使用的产品</summary>
    public class ProductStoreData
    {
        public List<string> Products { get; set; } = new List<string>();
        public string LastProduct { get; set; } = "Default";
    }

    /// <summary>
    /// 产品管理器：维护产品列表与当前产品，构建按产品分组的保存/加载路径。
    /// 路径结构：System/{当前产品名}/{ProcessPanel名}/
    /// 产品列表与上次产品统一存于 System/Products.dat（JSON 内容，.dat 后缀）。
    /// </summary>
    public static class ProductManager
    {
        private static string _currentProduct = "Default";
        /// <summary>LoadProducts 从 Products.dat 读出的上次产品，供 LoadLastProduct 应用</summary>
        private static string _loadedLastProduct = null;

        /// <summary>Products.dat 文件路径：{程序根}/System/Products.dat</summary>
        private static string ProductsFilePath =>
            Path.Combine(JsonDynamicHelper.GetAppRootPath(), "System", "Products.dat");

        /// <summary>当前产品名</summary>
        public static string CurrentProduct
        {
            get => _currentProduct;
            set => _currentProduct = string.IsNullOrWhiteSpace(value) ? "Default" : value.Trim();
        }

        /// <summary>当前ProcessPanel名（用于构建子目录）</summary>
        public static string CurrentProcessPanel { get; set; } = "ProcessPanel";

        /// <summary>所有产品名列表</summary>
        public static List<string> Products { get; private set; } = new List<string>();

        /// <summary>构建流程目录相对路径：System/{当前产品名}/{ProcessPanel名}</summary>
        public static string GetProcessDir()
        {
            return Path.Combine("System", CurrentProduct, CurrentProcessPanel);
        }

        /// <summary>当前产品的根目录（绝对路径）：{程序根}/System/{当前产品名}</summary>
        public static string CurrentProductPath =>
            Path.Combine(JsonDynamicHelper.GetAppRootPath(), "System", CurrentProduct);

        /// <summary>项目配置文件路径：{当前产品根}/Project.xml</summary>
        public static string ProjectConfigPath =>
            Path.Combine(CurrentProductPath, "Project.xml");

        /// <summary>获取指定流程名的目录（绝对路径）：{当前产品根}/{流程名}</summary>
        public static string GetProcessFolder(string processName)
        {
            return Path.Combine(CurrentProductPath, string.IsNullOrEmpty(processName) ? "Unnamed" : processName);
        }

        /// <summary>加载产品列表与上次产品（System/Products.dat）</summary>
        public static void LoadProducts()
        {
            try
            {
                if (File.Exists(ProductsFilePath))
                {
                    string json = File.ReadAllText(ProductsFilePath, Encoding.UTF8);
                    json = json.TrimStart();
                    if (json.StartsWith("["))
                    {
                        // 兼容旧格式：纯数组（无 LastProduct 字段）
                        var list = JsonConvert.DeserializeObject<List<string>>(json);
                        if (list != null) Products = list;
                        _loadedLastProduct = null;
                    }
                    else
                    {
                        // 新格式：{ Products, LastProduct }
                        var data = JsonConvert.DeserializeObject<ProductStoreData>(json);
                        if (data?.Products != null) Products = data.Products;
                        _loadedLastProduct = data?.LastProduct;
                    }
                }
            }
            catch { /* 加载失败使用默认列表 */ }

            if (Products == null) Products = new List<string>();
            if (Products.Count == 0)
                Products.Add("Default");
            if (string.IsNullOrEmpty(_currentProduct))
                _currentProduct = Products[0];
        }

        /// <summary>保存产品列表与当前产品（System/Products.dat）</summary>
        public static void SaveProducts()
        {
            try
            {
                string dir = Path.GetDirectoryName(ProductsFilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var data = new ProductStoreData
                {
                    Products = Products,
                    LastProduct = CurrentProduct
                };
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(ProductsFilePath, json, Encoding.UTF8);
            }
            catch { /* 忽略保存失败 */ }
        }

        /// <summary>记录当前产品名（直接写入 Products.dat，与产品列表同文件）</summary>
        public static void SaveLastProduct()
        {
            // CurrentProduct 已是最新值，保存即写入 Products.dat 的 LastProduct 字段
            SaveProducts();
        }

        /// <summary>恢复上次使用的产品（须在 LoadProducts 之后调用）</summary>
        public static void LoadLastProduct()
        {
            // 仅当记录的产品仍在产品列表中才恢复，避免指向已删除的产品
            if (!string.IsNullOrEmpty(_loadedLastProduct) && Products.Contains(_loadedLastProduct))
                CurrentProduct = _loadedLastProduct;
        }

        /// <summary>新增产品，可选择从源产品复制全部配置（克隆）</summary>
        /// <param name="name">新产品名</param>
        /// <param name="cloneFrom">克隆源产品名（为空则不复制）</param>
        public static void AddProduct(string name, string cloneFrom = null)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            name = name.Trim();
            if (!Products.Contains(name))
            {
                Products.Add(name);
                SaveProducts();
            }
            // 从源产品复制全部配置目录
            if (!string.IsNullOrWhiteSpace(cloneFrom) && Products.Contains(cloneFrom) && cloneFrom != name)
            {
                CloneProductData(cloneFrom, name);
            }
        }

        /// <summary>复制源产品的整个配置目录到新产品目录</summary>
        private static void CloneProductData(string sourceName, string destName)
        {
            try
            {
                string sourcePath = Path.Combine(JsonDynamicHelper.GetAppRootPath(), "System", sourceName);
                string destPath = Path.Combine(JsonDynamicHelper.GetAppRootPath(), "System", destName);
                if (!Directory.Exists(sourcePath)) return;
                // 目标已存在则先清空（避免残留旧数据混入）
                if (Directory.Exists(destPath))
                    Directory.Delete(destPath, true);
                CopyDirectory(sourcePath, destPath);
            }
            catch { /* 复制失败不阻断新增 */ }
        }

        /// <summary>递归复制目录</summary>
        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (string file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
            foreach (string dir in Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }

        /// <summary>删除产品</summary>
        public static void RemoveProduct(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (Products.Remove(name))
            {
                if (CurrentProduct == name)
                    CurrentProduct = Products.FirstOrDefault() ?? "Default";
                SaveProducts();
            }
        }
    }
}
