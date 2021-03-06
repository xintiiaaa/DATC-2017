﻿using AlbumPhoto.Models;
using AlbumPhoto.Service.Entities;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.DataServices;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web;

namespace AlbumPhoto.Service
{
	public class AlbumFotoService
	{
		private CloudStorageAccount _account;
		private CloudBlobClient _blobClient;
		private CloudBlobContainer _photoContainer;
		private CloudTableClient _tableClient;
		private CloudTable _filesTable;
		private CloudTable _commentsTable;
		private TableServiceContext _ctx;
        private BlobContainerPermissions containerPermissions;
        private static string SAS;

        public AlbumFotoService()
		{
			//_account = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("PhotoStorage"));
			_account = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
			_blobClient = _account.CreateCloudBlobClient();
			_photoContainer = _blobClient.GetContainerReference("poze");
			if (_photoContainer.CreateIfNotExists())
			{
				_photoContainer.SetPermissions(new BlobContainerPermissions() { PublicAccess = BlobContainerPublicAccessType.Blob });
			}
            containerPermissions = new BlobContainerPermissions();
            containerPermissions.SharedAccessPolicies.Add("twohourspolicy", new SharedAccessBlobPolicy()
            {
                SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-1),
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(2),
                Permissions = SharedAccessBlobPermissions.Read
            });
            containerPermissions.PublicAccess = BlobContainerPublicAccessType.Off;
            _photoContainer.SetPermissions(containerPermissions);
            SAS = _photoContainer.GetSharedAccessSignature(new SharedAccessBlobPolicy(), "twohourspolicy");

            _tableClient = _account.CreateCloudTableClient();
			_filesTable = _tableClient.GetTableReference("files");
			_filesTable.CreateIfNotExists();
			_commentsTable = _tableClient.GetTableReference("comments");
			_commentsTable.CreateIfNotExists();
			_ctx = _tableClient.GetTableServiceContext();
		}

		public List<Picture> Get_Picture()
		{
			var poze = new List<Picture>();
			var query = (from file in _ctx.CreateQuery<FileEntity>(_filesTable.Name)
						 select file).AsTableServiceQuery<FileEntity>(_ctx);

			foreach (var file in query)
			{
				poze.Add(new Picture()
				{
					Description = file.RowKey,
					ThumbnailUrl = file.ThumbnailUrl,
					Url = file.Url
				});
			}

			return poze;
		}

		public void Load_Picture(string userName, string description, Stream continut)
		{
			var blob = _photoContainer.GetBlockBlobReference(description);
			blob.UploadFromStream(continut);

			_ctx.AddObject(_filesTable.Name, new FileEntity(userName, description)
			{
				PublishDate = DateTime.UtcNow,
				Size = continut.Length,
				Url = blob.Uri.ToString(),
			});

			_ctx.SaveChangesWithRetries();
		}

        public void AddComment(string userName, string comment, string description)
        {
            _ctx.AddObject(_commentsTable.Name, new CommentEntity(userName, description)
            {
                Text = comment,
                MadeBy = userName
            });

            _ctx.SaveChangesWithRetries();
        }

        public static string GetSasBlobUrl(string fileName)
        {
            var storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudBlobClient sasBlobClient = new CloudBlobClient(storageAccount.BlobEndpoint, new StorageCredentials(SAS));
            CloudBlob blob = (CloudBlob)sasBlobClient.GetBlobReferenceFromServer(new Uri(fileName));
            return blob.Uri.AbsoluteUri + SAS;

            
        }

        public Picture GenerateLink(string pic)
        {
            String link = String.Empty;
            var poze = new List<Picture>();
            var query = (from file in _ctx.CreateQuery<FileEntity>(_filesTable.Name)
                         select file).AsTableServiceQuery<FileEntity>(_ctx);

            foreach (var file in query)
            {
                if (file.RowKey.Equals(pic))
                    poze.Add(new Picture()
                    {
                        Description = file.RowKey,
                        ThumbnailUrl = file.ThumbnailUrl,
                        Url = file.Url,
                        CommentsList = ViewComments(file.RowKey),
                        Link = GetSasBlobUrl(file.Url)
                    });
            }
            return poze.First();
        }

        public List<Comment> ViewComments(string description)
        {
            var comentarii = new List<Comment>();
            var query = (from com in _ctx.CreateQuery<CommentEntity>(_commentsTable.Name)
                         select com).Where(tab => tab.RowKey == description).AsTableServiceQuery<CommentEntity>(_ctx);

            foreach (var comment in query)
            {
                comentarii.Add(new Comment()
                {
                    Text = comment.Text,
                    CommentedBy = comment.MadeBy
                });
            }
            return comentarii;
        }
        public List<Like> ViewLike(string description)
        {
            var Likes= new List<Like>();
            var query = (from com in _ctx.CreateQuery<CommentEntity>(_commentsTable.Name)
                         select com).Where(tab => tab.RowKey == description).AsTableServiceQuery<CommentEntity>(_ctx);

            foreach (var comment in query)
            {
                Likes.Add(new Like()
                {
               
                });
            }
            return Likes;
        }




    }
}