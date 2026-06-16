<?php

namespace App\Http\Controllers\Admin;

use App\Http\Controllers\Controller;
use App\Models\Setting;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Illuminate\View\View;

class SettingController extends Controller
{
    /**
     * Editable site-wide settings. Adding a key here exposes it in the admin
     * form and makes it available via Setting::get() across the app.
     */
    public const KEYS = [
        'site_name_ar',
        'site_name_en',
        'adsense_client_id',
        'adsense_slot_in_article',
        'adsense_slot_mid_article',
        'adsense_slot_below_post',
        'adsense_slot_sidebar',
        'ads_txt_line',
        'analytics_id',
        'ai_disclosure_ar',
        'ai_disclosure_en',
        'contact_email',
    ];

    public function edit(): View
    {
        $settings = collect(self::KEYS)->mapWithKeys(fn ($key) => [
            $key => Setting::get($key),
        ]);

        return view('admin.settings', compact('settings'));
    }

    public function update(Request $request): RedirectResponse
    {
        foreach (self::KEYS as $key) {
            Setting::set($key, $request->input($key));
        }

        return back()->with('status', 'Settings saved.');
    }
}
