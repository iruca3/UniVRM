using System;
using System.Collections.Generic;
using System.Numerics;
using UniGLTF;
using Unity.Collections;

namespace UniVRM10
{
    public static class BufferAccessorAdapter
    {
        public static int TypeCount(this string type)
        {
            switch (type)
            {
                case "SCALAR":
                    return 1;
                case "VEC2":
                    return 2;
                case "VEC3":
                    return 3;
                case "VEC4":
                case "MAT2":
                    return 4;
                case "MAT3":
                    return 9;
                case "MAT4":
                    return 16;
                default:
                    throw new NotImplementedException();
            }
        }

        public static int AddViewTo(this BufferAccessor self,
            ExportingGltfData data, int bufferIndex,
            int offset = 0, int count = 0)
        {
            var stride = self.Stride;
            if (count == 0)
            {
                count = self.Count;
            }
            var slice = self.Bytes.GetSubArray(offset * stride, count * stride);
            return data.AppendToBuffer(slice);
        }

        static glTFAccessor CreateGltfAccessor(this BufferAccessor self,
            int viewIndex, int count = 0, int byteOffset = 0)
        {
            if (count == 0)
            {
                count = self.Count;
            }
            return new glTFAccessor
            {
                bufferView = viewIndex,
                byteOffset = byteOffset,
                componentType = (glComponentType)self.ComponentType,
                type = self.AccessorType.ToString(),
                count = count,
            };
        }

        public static int AddAccessorTo(this BufferAccessor self,
            ExportingGltfData data, int viewIndex,
            Action<NativeArray<byte>, glTFAccessor> minMax = null,
            int offset = 0, int count = 0)
        {
            var gltf = data.Gltf;
            var accessorIndex = gltf.accessors.Count;
            var accessor = self.CreateGltfAccessor(viewIndex, count, offset * self.Stride);
            if (minMax != null)
            {
                minMax(self.Bytes, accessor);
            }
            gltf.accessors.Add(accessor);
            return accessorIndex;
        }

        public static int AddAccessorTo(this BufferAccessor self,
            ExportingGltfData data, int bufferIndex,
            // GltfBufferTargetType targetType,
            bool useSparse,
            Action<NativeArray<byte>, glTFAccessor> minMax = null,
            int offset = 0, int count = 0)
        {
            if (self.ComponentType == AccessorValueType.FLOAT
            && self.AccessorType == AccessorVectorType.VEC3
            )
            {
                var values = self.GetSpan<Vector3>();
                // 巨大ポリゴンのモデル対策にValueTupleの型をushort -> uint へ
                var sparseValuesWithIndex = new List<ValueTuple<int, Vector3>>();
                for (int i = 0; i < values.Length; ++i)
                {
                    var v = values[i];
                    if (v != Vector3.Zero)
                    {
                        sparseValuesWithIndex.Add((i, v));
                    }
                }

                //var status = $"{sparseIndices.Count * 14}/{values.Length * 12}";
                if (useSparse
                && sparseValuesWithIndex.Count > 0 // avoid empty sparse
                && sparseValuesWithIndex.Count * 16 < values.Length * 12)
                {
                    // use sparse
                    using (var sparseIndexBin = new NativeArray<byte>(sparseValuesWithIndex.Count * 4, Allocator.Persistent))
                    using (var sparseValueBin = new NativeArray<byte>(sparseValuesWithIndex.Count * 12, Allocator.Persistent))
                    {
                        var sparseIndexSpan = sparseIndexBin.Reinterpret<Int32>(1);
                        var sparseValueSpan = sparseValueBin.Reinterpret<Vector3>(1);

                        for (int i = 0; i < sparseValuesWithIndex.Count; ++i)
                        {
                            var (index, value) = sparseValuesWithIndex[i];
                            sparseIndexSpan[i] = index;
                            sparseValueSpan[i] = value;
                        }

                        var sparseIndexView = data.AppendToBuffer(sparseIndexBin);
                        var sparseValueView = data.AppendToBuffer(sparseValueBin);

                        var accessorIndex = data.Gltf.accessors.Count;
                        var accessor = new glTFAccessor
                        {
                            componentType = (glComponentType)self.ComponentType,
                            type = self.AccessorType.ToString(),
                            count = self.Count,
                            byteOffset = -1,
                            sparse = new glTFSparse
                            {
                                count = sparseValuesWithIndex.Count,
                                indices = new glTFSparseIndices
                                {
                                    componentType = (glComponentType)AccessorValueType.UNSIGNED_INT,
                                    bufferView = sparseIndexView,
                                },
                                values = new glTFSparseValues
                                {
                                    bufferView = sparseValueView,
                                },
                            }
                        };
                        if (minMax != null)
                        {
                            minMax(sparseValueBin, accessor);
                        }
                        data.Gltf.accessors.Add(accessor);
                        return accessorIndex;
                    }
                }
            }

            var viewIndex = self.AddViewTo(data, bufferIndex, offset, count);
            return self.AddAccessorTo(data, viewIndex, minMax, 0, count);
        }
    }
}
