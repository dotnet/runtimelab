// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

package net.dot;

import android.app.Instrumentation;
import android.content.Context;
import android.content.pm.PackageInfo;
import android.content.pm.PackageManager;
import android.content.res.AssetManager;
import android.os.Bundle;
import android.os.Looper;
import android.util.Log;
import android.view.View;
import android.app.Activity;
import android.os.Environment;
import android.net.Uri;

import java.io.File;
import java.io.FileNotFoundException;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.io.BufferedInputStream;
import java.util.ArrayList;
import java.util.Calendar;
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;
import java.time.OffsetDateTime;
import java.time.ZoneOffset;

public class MonoRunner extends Instrumentation
{

    static String entryPointLibName = "%EntryPointLibName%";
    static Bundle result = new Bundle();
    static Context activityContext;

    private String[] argsToForward;

    @Override
    public void onCreate(Bundle arguments) {
        if (arguments != null) {
            ArrayList<String> argsList = new ArrayList<String>();
            for (String key : arguments.keySet()) {
                if (key.startsWith("env:")) {
                    String envName = key.substring("env:".length());
                    String envValue = arguments.getString(key);
                    setEnv(envName, envValue);
                    Log.i("DOTNET", "env:" + envName + "=" + envValue);
                } else if (key.equals("entrypoint:libname")) {
                    entryPointLibName = arguments.getString(key);
                } else {
                    String val = arguments.getString(key);
                    if (val != null) {
                        argsList.add(key);
                        argsList.add(val);
                    }
                }
            }

            argsToForward = argsList.toArray(new String[argsList.size()]);
        }

%EnvVariables%

        super.onCreate(arguments);
        start();
    }

    public static int initialize(String[] args, Context context) {
        System.loadLibrary("monodroid");
        Log.i("DOTNET", "MonoRunner initialize");
        activityContext = context;
        int retcode = initRuntime();

        return retcode;
    }

    @Override
    public void onStart() {
        Looper.prepare();
        int retcode = initialize(argsToForward, getContext());

        Log.i("DOTNET", "MonoRunner finished, return-code=" + retcode);
        result.putInt("return-code", retcode);

        finish(retcode, result);
    }

    public static void onClick() {
        OnButtonClick();
    }

    public static void setText(String text) {
        if (activityContext instanceof MainActivity) {
            MainActivity mainActivity = (MainActivity) activityContext;
            mainActivity.setText (text);
        }
    }

    static native int initRuntime();

    static native void OnButtonClick();

    static native int setEnv(String key, String value);
}
