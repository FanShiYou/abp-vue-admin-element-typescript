using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.FileSystem;
using Volo.Abp.IO;
using Volo.Abp.MultiTenancy;

namespace LINGYUN.Abp.OssManagement.FileSystem
{
    /// <summary>
    /// Oss容器的本地文件系统实现
    /// </summary>
    internal class FileSystemOssContainer : IOssContainer
    {
        protected ICurrentTenant CurrentTenant { get; }
        protected IHostEnvironment Environment { get; }
        protected IBlobFilePathCalculator FilePathCalculator { get; }
        protected IBlobContainerConfigurationProvider ConfigurationProvider { get; }
        protected IServiceProvider ServiceProvider { get; }
        protected FileSystemOssOptions Options { get; }

        public FileSystemOssContainer(
            ICurrentTenant currentTenant,
            IHostEnvironment environment,
            IServiceProvider serviceProvider,
            IBlobFilePathCalculator blobFilePathCalculator,
            IBlobContainerConfigurationProvider configurationProvider,
            IOptions<FileSystemOssOptions> options)
        {
            CurrentTenant = currentTenant;
            Environment = environment;
            ServiceProvider = serviceProvider;
            FilePathCalculator = blobFilePathCalculator;
            ConfigurationProvider = configurationProvider;
            Options = options.Value;
        }

        public virtual Task BulkDeleteObjectsAsync(BulkDeleteObjectRequest request)
        {
            var objectPath = !request.Path.IsNullOrWhiteSpace()
                ? request.Path.EnsureEndsWith('/')
                : "";
            var filesPath = request.Objects.Select(x => CalculateFilePath(request.Bucket, objectPath + x));

            foreach (var file in filesPath)
            {
                if (Directory.Exists(file))
                {
                    if (Directory.GetFileSystemEntries(file).Length > 0)
                    {
                        throw new BusinessException(code: OssManagementErrorCodes.ContainerDeleteWithNotEmpty);
                        // throw new ContainerDeleteWithNotEmptyException("00101", $"Can't not delete container {name}, because it is not empty!");
                    }
                    Directory.Delete(file);
                }
                else if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }

            return Task.CompletedTask;
        }

        public virtual Task<OssContainer> CreateAsync(string name)
        {
            var filePath = CalculateFilePath(name);
            ThrowOfPathHasTooLong(filePath);
            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
            }

            var directoryInfo = new DirectoryInfo(filePath);
            var container = new OssContainer(
                directoryInfo.Name,
                directoryInfo.CreationTime,
                0L,
                directoryInfo.LastWriteTime,
                new Dictionary<string, string>
                {
                    { "LastAccessTime",  directoryInfo.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss") }
                });

            return Task.FromResult(container);
        }

        public virtual async Task<OssObject> CreateObjectAsync(CreateOssObjectRequest request)
        {
            var objectPath = !request.Path.IsNullOrWhiteSpace()
                 ? request.Path.EnsureEndsWith('/')
                 : "";
            var objectName = objectPath.IsNullOrWhiteSpace()
                ? request.Object
                : objectPath + request.Object;

            var filePath = CalculateFilePath(request.Bucket, objectName);
            if (!request.Content.IsNullOrEmpty())
            {
                ThrowOfPathHasTooLong(filePath);

                FileMode fileMode = request.Overwrite ? FileMode.Create : FileMode.CreateNew;
                if (!request.Overwrite && File.Exists(filePath))
                {
                    throw new BusinessException(code: OssManagementErrorCodes.ObjectAlreadyExists);
                    // throw new OssObjectAlreadyExistsException($"Can't not put object {objectName} in container {request.Bucket}, Because a file with the same name already exists in the directory!");
                }

                DirectoryHelper.CreateIfNotExists(Path.GetDirectoryName(filePath));

                using (var fileStream = File.Open(filePath, fileMode, FileAccess.Write))
                {
                    await request.Content.CopyToAsync(fileStream);

                    await fileStream.FlushAsync();
                }
                var fileInfo = new FileInfo(filePath);
                var ossObject = new OssObject(
                fileInfo.Name,
                objectPath,
                fileInfo.CreationTime,
                fileInfo.Length,
                fileInfo.LastWriteTime,
                new Dictionary<string, string>
                {
                    { "IsReadOnly",  fileInfo.IsReadOnly.ToString() },
                    { "LastAccessTime",  fileInfo.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss") }
                })
                {
                    FullName = fileInfo.FullName.Replace(Environment.ContentRootPath, "")
                };
                ossObject.SetContent(request.Content);

                return ossObject;
            }
            else
            {
                ThrowOfPathHasTooLong(filePath);
                if (Directory.Exists(filePath))
                {
                    throw new BusinessException(code: OssManagementErrorCodes.ObjectAlreadyExists);
                    // throw new OssObjectAlreadyExistsException($"Can't not put object {objectName} in container {request.Bucket}, Because a file with the same name already exists in the directory!");
                }
                Directory.CreateDirectory(filePath);
                var directoryInfo = new DirectoryInfo(filePath);

                var ossObject = new OssObject(
                directoryInfo.Name.EnsureEndsWith('/'),
                objectPath,
                directoryInfo.CreationTime,
                0L,
                directoryInfo.LastWriteTime,
                new Dictionary<string, string>
                {
                    { "LastAccessTime",  directoryInfo.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss") }
                },
                true)
                {
                    FullName = directoryInfo.FullName.Replace(Environment.ContentRootPath, "")
                };
                ossObject.SetContent(request.Content);

                return ossObject;
            }
        }

        public virtual Task DeleteAsync(string name)
        {
            var filePath = CalculateFilePath(name);
            if (!Directory.Exists(filePath))
            {
                throw new BusinessException(code: OssManagementErrorCodes.ContainerNotFound);
            }
            // 非空目录无法删除
            if (Directory.GetFileSystemEntries(filePath).Length > 0)
            {
                throw new BusinessException(code: OssManagementErrorCodes.ContainerDeleteWithNotEmpty);
                // throw new ContainerDeleteWithNotEmptyException("00101", $"Can't not delete container {name}, because it is not empty!");
            }
            Directory.Delete(filePath);

            return Task.CompletedTask;
        }

        public virtual Task DeleteObjectAsync(GetOssObjectRequest request)
        {
            var objectName = request.Path.IsNullOrWhiteSpace()
                ? request.Object
                : request.Path.EnsureEndsWith('/') + request.Object;
            var filePath = CalculateFilePath(request.Bucket, objectName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            else if (Directory.Exists(filePath))
            {
                if (Directory.GetFileSystemEntries(filePath).Length > 0)
                {
                    throw new BusinessException(code: OssManagementErrorCodes.ObjectDeleteWithNotEmpty);
                }
                Directory.Delete(filePath);
            }

            return Task.CompletedTask;
        }

        public virtual Task<bool> ExistsAsync(string name)
        {
            var filePath = CalculateFilePath(name);

            return Task.FromResult(Directory.Exists(filePath));
        }

        public virtual Task<OssContainer> GetAsync(string name)
        {
            var filePath = CalculateFilePath(name);
            if (!Directory.Exists(filePath))
            {
                throw new BusinessException(code: OssManagementErrorCodes.ContainerNotFound);
                // throw new ContainerNotFoundException($"Can't not found container {name} in file system");
            }

            var directoryInfo = new DirectoryInfo(filePath);
            var container = new OssContainer(
                directoryInfo.Name,
                directoryInfo.CreationTime,
                0L,
                directoryInfo.LastWriteTime,
                new Dictionary<string, string>
                {
                    { "LastAccessTime",  directoryInfo.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss") }
                });

            return Task.FromResult(container);
        }

        public virtual async Task<OssObject> GetObjectAsync(GetOssObjectRequest request)
        {
            var objectPath = !request.Path.IsNullOrWhiteSpace()
                 ? request.Path.EnsureEndsWith('/')
                 : "";
            var objectName = objectPath.IsNullOrWhiteSpace()
                ? request.Object
                : objectPath + request.Object;

            var filePath = CalculateFilePath(request.Bucket, objectName);
            if (!File.Exists(filePath))
            {
                if (!Directory.Exists(filePath))
                {
                    throw new BusinessException(code: OssManagementErrorCodes.ObjectNotFound);
                    // throw new ContainerNotFoundException($"Can't not found object {objectName} in container {request.Bucket} with file system");
                }
                var directoryInfo = new DirectoryInfo(filePath);
                var ossObject = new OssObject(
                    directoryInfo.Name.EnsureEndsWith('/'),
                    objectPath,
                    directoryInfo.CreationTime,
                    0L,
                    directoryInfo.LastWriteTime,
                    new Dictionary<string, string>
                    { 
                        { "LastAccessTime",  directoryInfo.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss") }
                    },
                    true)
                {
                    FullName = directoryInfo.FullName.Replace(Environment.ContentRootPath, "")
                };
                return ossObject;
            }
            else
            {
                var fileInfo = new FileInfo(filePath);
                var ossObject = new OssObject(
                    fileInfo.Name,
                    objectPath,
                    fileInfo.CreationTime,
                    fileInfo.Length,
                    fileInfo.LastWriteTime,
                    new Dictionary<string, string>
                    {
                    { "IsReadOnly",  fileInfo.IsReadOnly.ToString() },
                    { "LastAccessTime",  fileInfo.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss") }
                    })
                {
                    FullName = fileInfo.FullName.Replace(Environment.ContentRootPath, "")
                };
                using (var fileStream = File.OpenRead(filePath))
                {
                    var memoryStream = new MemoryStream();
                    await fileStream.CopyToAsync(memoryStream);
                    ossObject.SetContent(memoryStream);

                    if (!request.Process.IsNullOrWhiteSpace())
                    {
                        using var serviceScope = ServiceProvider.CreateScope();
                        var context = new FileSystemOssObjectContext(request.Process, ossObject, serviceScope.ServiceProvider);
                        foreach (var processer in Options.Processers)
                        {
                            await processer.ProcessAsync(context);

                            if (context.Handled)
                            {
                                ossObject.SetContent(context.Content);
                                break;
                            }
                        }
                    }
                }

                return ossObject;
            }
        }

        public virtual Task<GetOssContainersResponse> GetListAsync(GetOssContainersRequest request)
        {
            // 不传递Bucket 检索根目录的Bucket
            var filePath = CalculateFilePath(null);

            // 获取根目录
            var directories = Directory.GetDirectories(filePath, request.Prefix ?? "*.*");

            // 排序目录
            Array.Sort(directories, delegate (string x, string y)
            {
                return x.CompareTo(y);
            });

            var spiltDirectories = directories;
            // 计算标记的位置进行截断
            if (!request.Marker.IsNullOrWhiteSpace())
            {
                var markIndex = directories.FindIndex(x => x.EndsWith(request.Marker));
                if (markIndex < 0)
                {
                    directories = new string[0];
                }
                else
                {
                    var markDirectories = new string[directories.Length - markIndex];
                    Array.Copy(directories, markIndex, markDirectories, 0, markDirectories.Length);
                    directories = markDirectories;
                }
            }
            // 需要截断最大的容器集合
            if (request.MaxKeys.HasValue)
            {
                spiltDirectories = directories.Take(request.MaxKeys ?? directories.Length).ToArray();
            }
            var nextDirectory = spiltDirectories.Length < directories.Length ? directories[spiltDirectories.Length] : "";
            if (!nextDirectory.IsNullOrWhiteSpace())
            {
                // 下一个标记的目录名称
                
                nextDirectory = new DirectoryInfo(nextDirectory).Name;
            }
            // 容器对应的目录信息集合
            var directoryInfos = spiltDirectories.Select(x => new DirectoryInfo(x));
            // 返回Oss容器描述集合
            var response = new GetOssContainersResponse(
                request.Prefix,
                request.Marker,
                nextDirectory,
                directories.Length,
                directoryInfos.Select(x => new OssContainer(
                    x.Name,
                    x.CreationTime,
                    0L,
                    x.LastWriteTime,
                    new Dictionary<string, string>
                    {
                        { "LastAccessTime",  x.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss") }
                    }))
                .ToList());

            return Task.FromResult(response);
        }

        public virtual Task<GetOssObjectsResponse> GetObjectsAsync(GetOssObjectsRequest request)
        {
            // 先定位检索的目录
            var filePath = CalculateFilePath(request.BucketName, request.Prefix);
            if (!Directory.Exists(filePath))
            {
                throw new BusinessException(code: OssManagementErrorCodes.ContainerNotFound);
                // throw new ContainerNotFoundException($"Can't not found container {request.BucketName} in file system");
            }
            // 目录也属于Oss对象,需要抽象的文件系统集合来存储
            var fileSystemNames = Directory.GetFileSystemEntries(filePath);
            int maxFilesCount = fileSystemNames.Length;

            // 排序所有文件与目录
            Array.Sort(fileSystemNames, delegate (string x, string y)
            {
                // 检索的是文件系统名称
                // 需要判断是否为文件夹进行升序排序
                // 参考 OssObjectComparer

                var xFolder = Directory.Exists(x);
                var yFolder = Directory.Exists(y);

                if (xFolder && yFolder)
                {
                    return x.CompareTo(y);
                }

                if (xFolder && !yFolder)
                {
                    return -1;
                }

                if (!xFolder && yFolder)
                {
                    return 1;
                }

                return x.CompareTo(y);
            });

            // 需要计算从哪个位置截断
            int markIndex = 0;
            if (!request.Marker.IsNullOrWhiteSpace())
            {
                markIndex = fileSystemNames.FindIndex(x => x.EndsWith(request.Marker));
                if (markIndex < 0)
                {
                    markIndex = 0;
                }
            }

            // 需要截断Oss对象列表
            var copyFileSystemNames = fileSystemNames;
            if (markIndex > 0)
            {
                copyFileSystemNames = fileSystemNames[(markIndex+1)..];
            }
            // 截取指定数量的Oss对象
            int maxResultCount = request.MaxKeys ?? 10;
            // Oss对象信息集合
            var fileSystems = copyFileSystemNames
                .Take(maxResultCount)
                .Select<string, FileSystemInfo>(file =>
                {
                    if (File.Exists(file))
                    {
                        return new FileInfo(file);
                    }
                    return new DirectoryInfo(file);
                })
                .ToArray();

            // 计算下一页起始标记文件/目录名称
            var nextMarkerIndex = fileSystemNames.FindIndex(x => x.EndsWith(fileSystems[fileSystems.Length - 1].Name));
            string nextMarker = "";
            if (nextMarkerIndex >=0 && nextMarkerIndex + 1 < fileSystemNames.Length)
            {
                nextMarker = fileSystemNames[nextMarkerIndex + 1];
                nextMarker = File.Exists(nextMarker)
                    ? new FileInfo(nextMarker).Name
                    : new DirectoryInfo(nextMarker).Name.EnsureEndsWith('/');
            }
            // 返回Oss对象描述集合
            var response = new GetOssObjectsResponse(
                request.BucketName,
                request.Prefix,
                request.Marker,
                nextMarker,
                "/", // 文件系统目录分隔符
                fileSystemNames.Length,
                fileSystems.Select(x => new OssObject(
                    (x is DirectoryInfo) ? x.Name.EnsureEndsWith('/') : x.Name,
                    request.Prefix,
                    x.CreationTime,
                    (x as FileInfo)?.Length ?? 0L,
                    x.LastWriteTime,
                    new Dictionary<string, string>
                    {
                        { "LastAccessTime",  x.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss") }
                    },
                    x is DirectoryInfo)
                {
                    FullName = x.FullName.Replace(Environment.ContentRootPath, "")
                })
                .ToList());

            return Task.FromResult(response);
        }

        protected virtual FileSystemBlobProviderConfiguration GetFileSystemConfiguration()
        {
            var configuration = ConfigurationProvider.Get<AbpOssManagementContainer>();
            var fileSystemConfiguration = configuration.GetFileSystemConfiguration();
            return fileSystemConfiguration;
        }

        protected virtual string CalculateFilePath(string bucketName, string blobName = "")
        {
            var fileSystemConfiguration = GetFileSystemConfiguration();
            var blobPath = fileSystemConfiguration.BasePath;

            if (CurrentTenant.Id == null)
            {
                blobPath = Path.Combine(blobPath, "host");
            }
            else
            {
                blobPath = Path.Combine(blobPath, "tenants", CurrentTenant.Id.Value.ToString("D"));
            }

            if (fileSystemConfiguration.AppendContainerNameToBasePath &&
                !bucketName.IsNullOrWhiteSpace())
            {
                blobPath = Path.Combine(blobPath, bucketName);
            }
            if (!blobName.IsNullOrWhiteSpace())
            {
                blobPath = Path.Combine(blobPath, blobName);
            }

            return blobPath;
        }

        private void ThrowOfPathHasTooLong(string path)
        {
            // Windows 133 260
            // Linux 255 4096
            //if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && path.Length >= 255) // 预留5位
            //{
            //    throw new BusinessException(code: OssManagementErrorCodes.OssNameHasTooLong);
            //}
        }
    }
}
