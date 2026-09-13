using System;
using HalconDotNet;

namespace HCore
{
    public class HImage : IDisposable
    {
        // 改为私有，禁止外部直接修改；通过SetImage()赋值
        private HObject _image;

        // 缓存元数据，图像切换时清空
        private int _cachedWidth;
        private int _cachedHeight;
        private int _cachedChannel;
        private bool _metaDirty = true;

        /// <summary>对外只读拿到原生HObject</summary>
        public HObject Image => _image;

        /// <summary>图像宽度</summary>
        public int Width
        {
            get
            {
                RefreshMetaIfNeed();
                
                return _cachedWidth;
            }
        }

        /// <summary>图像高度</summary>
        public int Height
        {
            get
            {
                RefreshMetaIfNeed();
                return _cachedHeight;
            }
        }

        /// <summary>通道数 1灰度，3彩色</summary>
        public int Channel
        {
            get
            {
                RefreshMetaIfNeed();
                return _cachedChannel;
            }
        }

        public bool IsEmpty => _image == null || !_image.IsInitialized();

        private HImage()
        {
            _image = new HObject();
            ResetMetaCache();
        }

        /// <summary>工厂方法：从HObject创建包装对象</summary>
        /// <param name="hObj">halcon图像</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static HImage Create(HObject hObj)
        {
            if (hObj == null || !hObj.IsInitialized())
                throw new ArgumentException("HObject图像未初始化");

            var img = new HImage();
            img._image = hObj;
            img._metaDirty = true;
            return img;
        }

        /// <summary>重新设置内部图像，自动标记元数据需要刷新</summary>
        public void SetImage(HObject hObj)
        {
            // 如果旧图像有效，注意业务层决定是否ClearObj；这里不自动释放，避免引用冲突
            _image = hObj;
            ResetMetaCache();
        }

        /// <summary>强制清空缓存标记，下次读取属性重新从图像获取宽高通道</summary>
        private void ResetMetaCache()
        {
            _cachedWidth = 0;
            _cachedHeight = 0;
            _cachedChannel = 0;
            _metaDirty = true;
        }

        /// <summary>需要的时候才调用算子刷新一次元数据，之后读缓存</summary>
        private void RefreshMetaIfNeed()
        {
            if (!_metaDirty) return;
            if (IsEmpty)
            {
                ResetMetaCache();
                return;
            }

            HTuple w, h, ch;
            HOperatorSet.GetImageSize(_image, out w, out h);
            HOperatorSet.CountChannels(_image, out ch);

            _cachedWidth = w.I;
            _cachedHeight = h.I;
            _cachedChannel = ch.I;

            _metaDirty = false;
        }

        #region IDisposable 释放halcon内存
        private bool _disposed = false;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                if (_image != null && _image.IsInitialized())
                {
                    HOperatorSet.ClearObj(_image);
                    _image = new HObject();
                }
                ResetMetaCache();
            }
            _disposed = true;
        }

        ~HImage()
        {
            Dispose(false);
        }
        #endregion

        // 可选：隐式转换，方便直接传给halcon算子
        public static implicit operator HObject(HImage wrapper)
        {
            return wrapper?._image ?? new HObject();
        }
    }
}