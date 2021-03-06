﻿/*
 * ******************************************************************************
 *   Copyright 2014 Spectra Logic Corporation. All Rights Reserved.
 *   Licensed under the Apache License, Version 2.0 (the "License"). You may not use
 *   this file except in compliance with the License. A copy of the License is located at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 *   or in the "license" file accompanying this file.
 *   This file is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
 *   CONDITIONS OF ANY KIND, either express or implied. See the License for the
 *   specific language governing permissions and limitations under the License.
 * ****************************************************************************
 */

using System.IO;

namespace Ds3.Runtime
{
    public class WebResponseStream : Stream
    {
        private readonly Stream _stream;
        private readonly long _contentLength;
        private long _bytesRead = 0;

        public WebResponseStream(Stream stream, long contentLength)
        {
            _stream = stream;
            _contentLength = contentLength;
        }

        public override bool CanRead
        {
            get
            {
                return _stream.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return _stream.CanSeek;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return _stream.CanWrite;
            }
        }

        public override long Length
        {
            get
            {
                return _stream.Length;
            }
        }

        public override long Position
        {
            get
            {
                return _stream.Position;
            }

            set
            {
                _stream.Position = value;
            }
        }

        public override void Flush()
        {
            _stream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _stream.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesRead = _stream.Read(buffer, offset, count);
            _bytesRead += bytesRead;

            if ( _contentLength > -1 && (bytesRead == 0) && (_bytesRead != _contentLength))
                throw new Ds3ContentLengthNotMatch(Resources.ContentLengthNotMatch, _contentLength, _bytesRead);

            return bytesRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(buffer, offset, count);
        }

        public long GetBytesRead()
        {
            return _bytesRead;
        }
    }
}
