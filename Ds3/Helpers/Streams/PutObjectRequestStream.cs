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

namespace Ds3.Helpers.Streams
{
    public class PutObjectRequestStream : Stream
    {
        private readonly Stream _stream;
        private long _streamLength;
        private long _totalBytesRead = 0;

        public PutObjectRequestStream(Stream stream, long offset, long length)
        {
            this._stream = stream;
            this._stream.Position = offset;
            this._streamLength = length;
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
                return _streamLength;
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
            return _stream.Seek(Position + offset, origin);
        }

        public override void SetLength(long value)
        {
            this._streamLength = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_totalBytesRead == this._streamLength)
            {
                return 0;
            }
            else if (_totalBytesRead + count > this._streamLength)
            {
                count = (int)_totalBytesRead + count - (int)this._streamLength;
            }

            int bytesRead = _stream.Read(buffer, offset, count);
            _totalBytesRead += bytesRead;
            return bytesRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(buffer, offset, count);
        }
    }
}
