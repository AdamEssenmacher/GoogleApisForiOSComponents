﻿using System;
using System.Collections.Generic;
namespace StorageSample
{
	public class Note
	{
		public string Node { get; set; }
		public string Title { get; set; }
		public string Content { get; set; }
		public string Created { get; set; }
		public string CreatedUnformatted { get; set; }
		public string LastModified { get; set; }
		public string LastModifiedUnformatted { get; set; }
		public List<ImageInfo> ImagesInfo;
	}
}
