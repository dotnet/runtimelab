// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

package net.dot;

import android.app.Activity;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.widget.RelativeLayout;
import android.widget.TextView;
import android.widget.Button;
import android.graphics.Color;
import android.view.View;
import net.dot.AndroidSampleApp.R;

public class MainActivity extends Activity
{
    private static TextView textView;
    private static Button button;

    public static long startTime;
    public static long endTime;    


    @Override
    protected void onCreate(Bundle savedInstanceState)
    {
        super.onCreate(savedInstanceState);

        setContentView(R.layout.activity_main);
        button = (Button) findViewById(R.id.button1);

        button.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
                MonoRunner.onClick();
            }
        });

        final Activity ctx = this;
        new Handler(Looper.getMainLooper()).postDelayed(new Runnable() {
            @Override
            public void run() {
                startTime = System.nanoTime();
                int retcode = MonoRunner.initialize(new String[0], ctx);
                endTime = System.nanoTime();
                System.out.println ("Runtime startup time: "+ ((endTime - startTime) / 1000 / 1000) + " ms");
            }
        }, 1000);
    }

    public static void setText (String text) {
        button.setText (text);
    }
}
