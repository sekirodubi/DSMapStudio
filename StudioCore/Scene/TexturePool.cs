﻿using System;
using System.Collections.Generic;
using System.Text;
using SoulsFormats;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Veldrid;

namespace StudioCore.Scene
{
    /// <summary>
    /// Low level texture pool that maintains an array of descriptor sets that can
    /// be bound to a shader.
    /// </summary>
    public class TexturePool
    {
        private static PixelFormat GetPixelFormatFromFourCC(string str)
        {
            switch (str)
            {
                case "DXT1":
                    return PixelFormat.BC1_Rgba_UNorm_SRgb;
                case "DXT3":
                    return PixelFormat.BC2_UNorm_SRgb;
                case "DXT5":
                    return PixelFormat.BC3_UNorm_SRgb;
                case "ATI1":
                    return PixelFormat.BC4_UNorm; // Monogame workaround :fatcat:
                case "ATI2":
                    return PixelFormat.BC5_UNorm;
                default:
                    throw new Exception($"Unknown DDS Type: {str}");
            }
        }

        private static PixelFormat GetPixelFormatFromDXGI(DDS.DXGI_FORMAT fmt)
        {
            switch (fmt)
            {
                case DDS.DXGI_FORMAT.B8G8R8A8_TYPELESS:
                case DDS.DXGI_FORMAT.B8G8R8A8_UNORM:
                case DDS.DXGI_FORMAT.B8G8R8X8_TYPELESS:
                case DDS.DXGI_FORMAT.B8G8R8X8_UNORM:
                    return PixelFormat.B8_G8_R8_A8_UNorm;
                case DDS.DXGI_FORMAT.B8G8R8A8_UNORM_SRGB:
                case DDS.DXGI_FORMAT.B8G8R8X8_UNORM_SRGB:
                    return PixelFormat.B8_G8_R8_A8_UNorm_SRgb;
                case DDS.DXGI_FORMAT.BC1_TYPELESS:
                case DDS.DXGI_FORMAT.BC1_UNORM:
                    return PixelFormat.BC1_Rgba_UNorm;
                case DDS.DXGI_FORMAT.BC1_UNORM_SRGB:
                    return PixelFormat.BC1_Rgba_UNorm_SRgb;
                case DDS.DXGI_FORMAT.BC2_TYPELESS:
                case DDS.DXGI_FORMAT.BC2_UNORM:
                    return PixelFormat.BC2_UNorm;
                case DDS.DXGI_FORMAT.BC2_UNORM_SRGB:
                    return PixelFormat.BC2_UNorm_SRgb;
                case DDS.DXGI_FORMAT.BC3_TYPELESS:
                case DDS.DXGI_FORMAT.BC3_UNORM:
                    return PixelFormat.BC3_UNorm;
                case DDS.DXGI_FORMAT.BC3_UNORM_SRGB:
                    return PixelFormat.BC3_UNorm_SRgb;
                case DDS.DXGI_FORMAT.BC4_TYPELESS:
                case DDS.DXGI_FORMAT.BC4_UNORM:
                    return PixelFormat.BC4_UNorm;
                case DDS.DXGI_FORMAT.BC4_SNORM:
                    return PixelFormat.BC4_SNorm;
                case DDS.DXGI_FORMAT.BC5_TYPELESS:
                case DDS.DXGI_FORMAT.BC5_UNORM:
                    return PixelFormat.BC5_UNorm;
                case DDS.DXGI_FORMAT.BC5_SNORM:
                    return PixelFormat.BC5_SNorm;
                case DDS.DXGI_FORMAT.BC6H_TYPELESS:
                case DDS.DXGI_FORMAT.BC6H_UF16:
                    return PixelFormat.BC6H_UFloat;
                case DDS.DXGI_FORMAT.BC6H_SF16:
                    return PixelFormat.BC6H_SFloat;
                case DDS.DXGI_FORMAT.BC7_TYPELESS:
                case DDS.DXGI_FORMAT.BC7_UNORM:
                    return PixelFormat.BC7_UNorm;
                case DDS.DXGI_FORMAT.BC7_UNORM_SRGB:
                    return PixelFormat.BC7_UNorm_SRgb;
                default:
                    throw new Exception($"Unimplemented DXGI Type: {fmt.ToString()}");
            }
        }

        // From MonoGame.Framework/Graphics/Texture2D.cs and MonoGame.Framework/Graphics/TextureCube.cs
        //private static (int ByteCount, Rectangle Rect) GetMipInfo(PixelFormat sf, int width, int height, int mip, bool isCubemap)
        private static int GetMipInfo(PixelFormat sf, int width, int height, int mip, bool isCubemap)
        {
            width = Math.Max(width >> mip, 1);
            height = Math.Max(height >> mip, 1);

            int formatTexelSize = GetTexelSize(sf);

            if (isCubemap)
            {
                if (IsCompressedFormat(sf))
                {
                    var roundedWidth = (width + 3) & ~0x3;
                    var roundedHeight = (height + 3) & ~0x3;

                    int byteCount = roundedWidth * roundedHeight * formatTexelSize / 16;

                    //return (byteCount, new Rectangle(0, 0, roundedWidth, roundedHeight));
                    return byteCount;
                }
                else
                {
                    int byteCount = width * height * formatTexelSize;

                    return byteCount;
                    //return (byteCount, new Rectangle(0, 0, width, height));
                }
            }
            else
            {
                if (IsCompressedFormat(sf))
                {
                    int blockWidth, blockHeight;
                    GetBlockSize(sf, out blockWidth, out blockHeight);

                    int blockWidthMinusOne = blockWidth - 1;
                    int blockHeightMinusOne = blockHeight - 1;

                    var roundedWidth = (width + blockWidthMinusOne) & ~blockWidthMinusOne;
                    var roundedHeight = (height + blockHeightMinusOne) & ~blockHeightMinusOne;

                    var rect = new Rectangle(0, 0, roundedWidth, roundedHeight);

                    int byteCount;

                    byteCount = roundedWidth * roundedHeight * formatTexelSize / (blockWidth * blockHeight);

                    //return (byteCount, rect);
                    return byteCount;
                }
                else
                {
                    int byteCount = width * height * formatTexelSize;

                    //return (byteCount, new Rectangle(0, 0, width, height));
                    return byteCount;
                }


            }

        }

        internal static int GetBlockSize(byte tpfTexFormat)
        {
            switch (tpfTexFormat)
            {
                case 105:
                    return 4;
                case 0:
                case 1:
                case 22:
                case 25:
                case 103:
                case 108:
                case 109:
                    return 8;
                case 5:
                case 100:
                case 102:
                case 106:
                case 107:
                case 110:
                    return 16;
                default:
                    throw new NotImplementedException($"TPF Texture format {tpfTexFormat} BlockSize unknown.");
            }
        }

        public static bool IsCompressedFormat(PixelFormat format)
        {
            switch (format)
            {
                case PixelFormat.BC1_Rgba_UNorm:
                case PixelFormat.BC1_Rgba_UNorm_SRgb:
                case PixelFormat.BC1_Rgb_UNorm:
                case PixelFormat.BC1_Rgb_UNorm_SRgb:
                case PixelFormat.BC2_UNorm:
                case PixelFormat.BC2_UNorm_SRgb:
                case PixelFormat.BC3_UNorm:
                case PixelFormat.BC3_UNorm_SRgb:
                case PixelFormat.BC4_UNorm:
                case PixelFormat.BC4_SNorm:
                case PixelFormat.BC5_UNorm:
                case PixelFormat.BC5_SNorm:
                case PixelFormat.BC6H_SFloat:
                case PixelFormat.BC6H_UFloat:
                case PixelFormat.BC7_UNorm:
                case PixelFormat.BC7_UNorm_SRgb:
                    return true;
            }
            return false;
        }

        public static void GetBlockSize(PixelFormat surfaceFormat, out int width, out int height)
        {
            switch (surfaceFormat)
            {
                case PixelFormat.BC1_Rgba_UNorm:
                case PixelFormat.BC1_Rgba_UNorm_SRgb:
                case PixelFormat.BC1_Rgb_UNorm:
                case PixelFormat.BC1_Rgb_UNorm_SRgb:
                case PixelFormat.BC2_UNorm:
                case PixelFormat.BC2_UNorm_SRgb:
                case PixelFormat.BC3_UNorm:
                case PixelFormat.BC3_UNorm_SRgb:
                case PixelFormat.BC4_UNorm:
                case PixelFormat.BC4_SNorm:
                case PixelFormat.BC5_UNorm:
                case PixelFormat.BC5_SNorm:
                case PixelFormat.BC6H_SFloat:
                case PixelFormat.BC6H_UFloat:
                case PixelFormat.BC7_UNorm:
                case PixelFormat.BC7_UNorm_SRgb:
                    width = 4;
                    height = 4;
                    break;
                default:
                    width = 1;
                    height = 1;
                    break;
            }
        }

        public static int GetTexelSize(PixelFormat surfaceFormat)
        {
            switch (surfaceFormat)
            {
                case PixelFormat.BC1_Rgba_UNorm:
                case PixelFormat.BC1_Rgba_UNorm_SRgb:
                case PixelFormat.BC1_Rgb_UNorm:
                case PixelFormat.BC1_Rgb_UNorm_SRgb:
                case PixelFormat.BC4_UNorm:
                case PixelFormat.BC4_SNorm:
                case PixelFormat.BC5_UNorm:
                case PixelFormat.BC5_SNorm:
                    return 8;
                case PixelFormat.BC2_UNorm:
                case PixelFormat.BC2_UNorm_SRgb:
                case PixelFormat.BC3_UNorm:
                case PixelFormat.BC3_UNorm_SRgb:
                case PixelFormat.BC6H_SFloat:
                case PixelFormat.BC6H_UFloat:
                case PixelFormat.BC7_UNorm:
                case PixelFormat.BC7_UNorm_SRgb:
                    return 16;
                case PixelFormat.R8_UNorm:
                case PixelFormat.R8_SNorm:
                case PixelFormat.R8_UInt:
                case PixelFormat.R8_SInt:
                    return 1;
                case PixelFormat.R16_UNorm:
                case PixelFormat.R16_SNorm:
                case PixelFormat.R8_G8_SInt:
                case PixelFormat.R8_G8_SNorm:
                case PixelFormat.R8_G8_UInt:
                case PixelFormat.R8_G8_UNorm:
                    return 2;
                case PixelFormat.R8_G8_B8_A8_SInt:
                case PixelFormat.R8_G8_B8_A8_SNorm:
                case PixelFormat.R8_G8_B8_A8_UInt:
                case PixelFormat.R8_G8_B8_A8_UNorm:
                case PixelFormat.R8_G8_B8_A8_UNorm_SRgb:
                case PixelFormat.B8_G8_R8_A8_UNorm:
                case PixelFormat.B8_G8_R8_A8_UNorm_SRgb:
                    return 4;
                //case PixelFormat.R16_G16_B16_A16_Float:
                //    return 8;
                //case SurfaceFormat.Vector4:
                //    return 16;
                default:
                    throw new ArgumentException();
            }
        }

        public uint TextureCount { get; private set; } = 0;
        private List<TextureHandle> _handles = new List<TextureHandle>();

        private ResourceLayout _poolLayout = null;
        private ResourceSet _poolResourceSet = null;
        private string _resourceName = null;

        private object _allocationLock = new object();

        public bool DescriptorTableDirty { get; private set; } = false;

        public TexturePool(GraphicsDevice d, string name, uint poolsize)
        {
            _resourceName = name;
            TextureCount = poolsize;

            var layoutdesc = new ResourceLayoutDescription(
                new ResourceLayoutElementDescription(_resourceName, ResourceKind.TextureReadOnly, ShaderStages.Fragment, TextureCount));
            _poolLayout = d.ResourceFactory.CreateResourceLayout(layoutdesc);
        }

        public TextureHandle AllocateTextureDescriptor()
        {
            TextureHandle handle;
            lock (_allocationLock)
            {
                handle = new TextureHandle(this, (uint)_handles.Count);
                _handles.Add(handle);
                //TextureCount++;
            }
            return handle;
        }

        public void RegenerateDescriptorTables()
        {
            Renderer.AddBackgroundUploadTask((d, cl) =>
            {
                lock (_allocationLock)
                {
                    if (TextureCount == 0)
                    {
                        return;
                    }
                    if (_poolLayout != null)
                    {
                        //_poolLayout.Dispose();
                    }
                    if (_poolResourceSet != null)
                    {
                        _poolResourceSet.Dispose();
                    }

                    //var layoutdesc = new ResourceLayoutDescription(
                    //    new ResourceLayoutElementDescription(_resourceName, ResourceKind.TextureReadOnly, ShaderStages.Fragment, TextureCount));
                    //_poolLayout = d.ResourceFactory.CreateResourceLayout(layoutdesc);

                    BindableResource[] resources = new BindableResource[TextureCount];
                    for (int i = 0; i < TextureCount; i++)
                    {
                        if (i >= _handles.Count)
                        {
                            resources[i] = _handles[0]._texture;
                            continue;
                        }
                        resources[i] = _handles[i]._texture;
                        if (_handles[i]._texture == null)
                        {
                            resources[i] = _handles[0]._texture;
                        }
                    }
                    _poolResourceSet = d.ResourceFactory.CreateResourceSet(new ResourceSetDescription(_poolLayout, resources));
                    DescriptorTableDirty = false;
                }
            });

        }

        public ResourceLayout GetLayout()
        {
            return _poolLayout;
        }

        public void BindTexturePool(CommandList cl, uint slot)
        {
            if (_poolResourceSet != null)
            {
                cl.SetGraphicsResourceSet(slot, _poolResourceSet);
            }
        }

        public void CleanTexturePool()
        {
            lock (_allocationLock)
            {
                foreach (var t in _handles)
                {
                    t.Clean();
                }
            }
        }

        public class TextureHandle : IDisposable
        {
            private TexturePool _pool;
            internal Texture _staging = null;
            internal Texture _texture = null;

            public uint TexHandle { get; private set; }

            public bool Resident { get; private set; } = false;

            public TextureHandle(TexturePool pool, uint handle)
            {
                _pool = pool;
                TexHandle = handle;
            }

            public unsafe void FillWithTPF(GraphicsDevice d, CommandList cl, TPF.TPFPlatform platform, TPF.Texture tex)
            {
                if (platform != TPF.TPFPlatform.PC)
                {
                    return;
                }
                DDS dds = new DDS(tex.Bytes);

                uint width = (uint)dds.dwWidth;
                uint height = (uint)dds.dwHeight;
                PixelFormat format;
                if (dds.header10 != null)
                {
                    format = GetPixelFormatFromDXGI(dds.header10.dxgiFormat);
                }
                else
                {
                    format = GetPixelFormatFromFourCC(dds.ddspf.dwFourCC);
                }
                if (platform == TPF.TPFPlatform.PC)
                {
                    width = IsCompressedFormat(format) ? (uint)((width + 3) & ~0x3) : width;
                    height = IsCompressedFormat(format) ? (uint)((height + 3) & ~0x3) : height;
                }

                bool isCubemap = false;
                if (platform == TPF.TPFPlatform.PC)
                {
                    if ((dds.dwCaps2 & DDS.DDSCAPS2.CUBEMAP) > 0)
                    {
                        isCubemap = true;
                    }
                }

                var usage = (isCubemap) ? TextureUsage.Cubemap : 0;

                uint arrayCount = isCubemap ? 6u : 1;

                TextureDescription desc = new TextureDescription();
                desc.Width = width;
                desc.Height = height;
                desc.MipLevels = (uint)dds.dwMipMapCount;
                desc.SampleCount = TextureSampleCount.Count1;
                desc.ArrayLayers = arrayCount;
                desc.Depth = 1;
                desc.Type = TextureType.Texture2D;
                desc.Usage = TextureUsage.Staging;
                desc.Format = format;

                _staging = d.ResourceFactory.CreateTexture(desc);

                int paddedWidth = 0;
                int paddedHeight = 0;
                int paddedSize = 0;
                int copyOffset = dds.DataOffset;

                for (int slice = 0; slice < arrayCount; slice++)
                {
                    for (uint level = 0; level < dds.dwMipMapCount; level++)
                    {
                        MappedResource map = d.Map(_staging, MapMode.Write, (uint)slice * (uint)dds.dwMipMapCount + level);
                        var mipInfo = GetMipInfo(format, (int)dds.dwWidth, (int)dds.dwHeight, (int)level, false);
                        //paddedSize = mipInfo.ByteCount;
                        paddedSize = mipInfo;
                        fixed (void* data = &tex.Bytes[copyOffset])
                        {
                            Unsafe.CopyBlock(map.Data.ToPointer(), data, (uint)paddedSize);
                        }
                        copyOffset += paddedSize;
                    }
                }

                desc.Usage = TextureUsage.Sampled | usage;
                desc.ArrayLayers = 1;
                _texture = d.ResourceFactory.CreateTexture(desc);
                cl.CopyTexture(_staging, _texture);
                Resident = true;
                _pool.DescriptorTableDirty = true;
            }

            public unsafe void FillWithColor(GraphicsDevice d, System.Drawing.Color c)
            {
                TextureDescription desc = new TextureDescription();
                desc.Width = 1;
                desc.Height = 1;
                desc.MipLevels = 1;
                desc.SampleCount = TextureSampleCount.Count1;
                desc.ArrayLayers = 1;
                desc.Depth = 1;
                desc.Type = TextureType.Texture2D;
                desc.Usage = TextureUsage.Staging;
                desc.Format = PixelFormat.R8_G8_B8_A8_UNorm;
                _staging = d.ResourceFactory.CreateTexture(desc);

                byte[] col = new byte[4];
                col[0] = c.R;
                col[1] = c.G;
                col[2] = c.B;
                col[3] = c.A;
                MappedResource map = d.Map(_staging, MapMode.Write, 0);
                fixed (void* data = col)
                {
                    Unsafe.CopyBlock(map.Data.ToPointer(), data, 4);
                }

                _pool.DescriptorTableDirty = true;

                Renderer.AddBackgroundUploadTask((gd, cl) =>
                {
                    desc.Usage = TextureUsage.Sampled;
                    _texture = d.ResourceFactory.CreateTexture(desc);
                    cl.CopyTexture(_staging, _texture);
                    Resident = true;
                    _pool.DescriptorTableDirty = true;
                });
            }

            public unsafe void FillWithColorCube(GraphicsDevice d, System.Numerics.Vector4 c)
            {
                TextureDescription desc = new TextureDescription();
                desc.Width = 1;
                desc.Height = 1;
                desc.MipLevels = 1;
                desc.SampleCount = TextureSampleCount.Count1;
                desc.ArrayLayers = 6;
                desc.Depth = 1;
                desc.Type = TextureType.Texture2D;
                desc.Usage = TextureUsage.Staging;
                desc.Format = PixelFormat.R32_G32_B32_A32_Float;
                _staging = d.ResourceFactory.CreateTexture(desc);

                float[] col = new float[4];
                col[0] = c.X;
                col[1] = c.Y;
                col[2] = c.Z;
                col[3] = c.W;
                for (uint i = 0; i < 6; i++)
                {
                    MappedResource map = d.Map(_staging, MapMode.Write, i);
                    fixed (void* data = col)
                    {
                        Unsafe.CopyBlock(map.Data.ToPointer(), data, 16);
                    }
                }

                _pool.DescriptorTableDirty = true;

                Renderer.AddBackgroundUploadTask((gd, cl) =>
                {
                    desc.ArrayLayers = 1;
                    desc.Usage = TextureUsage.Sampled | TextureUsage.Cubemap;
                    _texture = d.ResourceFactory.CreateTexture(desc);
                    cl.CopyTexture(_staging, _texture);
                    Resident = true;
                    _pool.DescriptorTableDirty = true;
                });
            }

            public unsafe void FillWithGPUTexture(Texture texture)
            {
                if (_texture != null)
                {
                    _texture.Dispose();
                }
                _texture = texture;
                Resident = true;
                _pool.DescriptorTableDirty = true;
            }

            public void Clean()
            {
                if (Resident && _staging != null)
                {
                    _staging.Dispose();
                    _staging = null;
                }
            }

            #region IDisposable Support
            private bool disposedValue = false; // To detect redundant calls

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        // TODO: dispose managed state (managed objects).
                    }

                    if (_texture != null)
                    {
                        _texture.Dispose();
                        _texture = null;
                    }

                    disposedValue = true;
                }
            }

            ~TextureHandle()
            {
                Dispose(false);
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
            #endregion
        }
    }
}