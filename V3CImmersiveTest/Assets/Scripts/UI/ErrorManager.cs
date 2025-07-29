/*
* Copyright (c) 2025 InterDigital CE Patent Holdings SASU
* Licensed under the License terms of 5GMAG software (the "License").
* You may not use this file except in compliance with the License.
* You may obtain a copy of the License at https://www.5g-mag.com/license .
* Unless required by applicable law or agreed to in writing, software distributed under the License is
* distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and limitations under the License.
*/

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ErrorManager : MonoBehaviour
{
    public Text m_title;
    public Text m_message;

    private uint m_errorLevel = 0;
    private uint m_errorId = 0;

    public void SetError(uint errorLevel, uint errorId)
    {
        m_errorLevel = errorLevel;
        m_errorId = errorId;

        switch (m_errorLevel)
        {
            case 0:
                m_title.text = "Info";
                break;
            case 1:
                m_title.text = "Warning";
                break;
            case 2:
                m_title.text = "Error";
                break;
        }

        m_message.text = "An error has been raised !\n";
        //enum class errStreamer
        //{
        //    DASHCLIENT_OK = 0,                    // "no error"
        //    DASHCLIENT_INIT_FAILED,               // "initialisation failed"
        //    DASHCLIENT_INIT_SEGMENT_EMPTY,        // "the init segment is empty"
        //    DASHCLIENT_ACCESS_TO_MPD_FAILED,      // "can not access to the MPD file"
        //    DASHCLIENT_PARSE_MPD_FAILED,          // "parsing MPD file failed"
        //    DASHCLIENT_MPD_NOT_FOUND,             // "MPD file not found"
        //    DASHCLIENT_CURL_CONNECTION_ERROR,     // "CURL connection error"
        //    DASHCLIENT_HTTP_ERROR,                // "HTTP error"
        //    DASHCLIENT_STREAMER_ERROR,            // "streamer error"
        //    DASHCLIENT_EXTRACTION_ERROR,          // "extraction error"
        //    DASHCLIENT_HJIF_ERROR,                // "HJIF error"
        //    DASHCLIENT_HJIF_NOT_FOUND,            // "HJIF file not found"
        //    DASHCLIENT_ACCESS_TO_HJIF_FAILED,     // "access to hjif file failed"
        //    DASHCLIENT_CONFIG_NOT_FOUND,          // "config file for haptic not found"
        //    DASHCLIENT_NO_HAPTIC,                 // "no haptic"
        //    DASHCLIENT_INITSEGMENT_NOT_FOUND,     // "init segment not found"
        //    DASHCLIENT_SEGMENT_NOT_FOUND,         // "segment not found"
        //    DASHCLIENT_ACCESS_TO_SEGMENT_FAILED,  // "access to segment failed"
        //    DASHCLIENT_UNKNOWN                    // "unknown error"
        //    LOCAL_OK = 100,                       // "no error occurred"
        //    LOCAL_INVALID_FILE                    // "invalid stream file"
        //    LOCAL_UNKNOWN                         // "unknown error"
        //    DECODER_OK = 200,                     // "no decoder error"
        //    DECODER_NO_AVCODEC                    // "avcodec file undefined"
        //    DECODER_UNKNOWN                       // "unknown error"
        //};
        switch (m_errorId)
        {
            case 0:
                m_message.text += "No error";
                break;
            case 1:
                m_message.text += "Initialisation failed";
                break;
            case 2:
                m_message.text += "The init segment is empty";
                break;
            case 3:
                m_message.text += "Can not access to the MPD file";
                break;
            case 4:
                m_message.text += "Parsing MPD file failed";
                break;
            case 5:
                m_message.text += "MPD file not found";
                break;
            case 6:
                m_message.text += "CURL connection error";
                break;
            case 7:
                m_message.text += "HTTP error";
                break;
            case 8:
                m_message.text += "Streamer error";
                break;
            case 9:
                m_message.text += "Extraction error";
                break;
            case 10:
                m_message.text += "HJIF error";
                break;
            case 11:
                m_message.text += "HJIF file not found";
                break;
            case 12:
                m_message.text += "Access to HJIF file failed";
                break;
            case 13:
                m_message.text += "Config file for haptic not found";
                break;
            case 14:
                m_message.text += "No haptic";
                break;
            case 15:
                m_message.text += "Init segment not found";
                break;
            case 16:
                m_message.text += "Segment not found";
                break;
            case 17:
                m_message.text += "Access to segment failed";
                break;
            case 100:
                m_message.text += "No error";
                break;
            case 101:
                m_message.text += "Invalid stream file";
                break;
            case 200:
                m_message.text += "No decoder error";
                break;
            case 201:
                m_message.text += "AVCODEC file undefined";
                break;
            default:
                m_message.text += "Unknown error";
                break;
        }
    }

    public void ShowErrorMenu()
    {
        switch (m_errorLevel)
        {
            case 0:
            case 1:
                StartCoroutine(DisplayForSeconds(4));
                break;
            case 2:
                StartCoroutine(DisplayForSeconds(7));
                break;
        }
    }

    IEnumerator DisplayForSeconds(uint seconds)
    {
        yield return new WaitForSeconds(seconds);
        gameObject.SetActive(false);
    }
}
