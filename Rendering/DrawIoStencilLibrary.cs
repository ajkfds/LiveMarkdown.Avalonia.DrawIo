namespace LiveMarkdown.Avalonia;

/// <summary>
/// Built-in stencil-set XML embedded with the package so the generic
/// <see cref="DrawIoStencil"/> engine can render common flowchart / UML shapes
/// without shipping external data files.
///
/// <para>These are hand-ported subsets of draw.io's stencil sets (Apache-2.0
/// compatible grammar). Additional sets can be registered at runtime through
/// <see cref="DrawIoStencil.RegisterStencilSet"/>.</para>
/// </summary>
internal static class DrawIoStencilLibrary
{
    /// <summary>
    /// Registers the bundled stencil sets. Idempotent (registration overwrites
    /// existing definitions), so it can be called every time a diagram is built.
    /// </summary>
    public static void RegisterBuiltIns()
    {
        DrawIoStencil.RegisterStencilSet(Flowchart, "mxgraph.flowchart.");
    }

    /// <summary>
    /// draw.io <c>flowchart.xml</c> subset. View boxes are 100x100 unless noted.
    /// </summary>
    private const string Flowchart = """
        <shapes name="mxgraph.flowchart">
          <shape name="terminator" w="100" h="100" strokewidth="1">
            <foreground>
              <path>
                <move x="0" y="50"/>
                <curve x1="0" y1="22" x2="22" y2="0" x3="50" y3="0"/>
                <line x="50" y="0"/>
                <curve x1="78" y1="0" x2="100" y2="22" x3="100" y3="50"/>
                <curve x1="100" y1="78" x2="78" y2="100" x3="50" y3="100"/>
                <line x="50" y="100"/>
                <curve x1="22" y1="100" x2="0" y2="78" x3="0" y3="50"/>
                <close/>
                <fillstroke/>
              </path>
            </foreground>
          </shape>

          <shape name="process" w="100" h="100" strokewidth="1">
            <foreground>
              <path>
                <rect x="0" y="0" w="100" h="100"/>
                <fillstroke/>
              </path>
            </foreground>
          </shape>

          <shape name="decision" w="100" h="100" strokewidth="1">
            <foreground>
              <path>
                <move x="50" y="0"/>
                <line x="100" y="50"/>
                <line x="50" y="100"/>
                <line x="0" y="50"/>
                <close/>
                <fillstroke/>
              </path>
            </foreground>
          </shape>

          <shape name="predefined_process" w="100" h="100" strokewidth="1">
            <foreground>
              <path>
                <rect x="0" y="0" w="100" h="100"/>
                <fillstroke/>
              </path>
              <path>
                <move x="10" y="0"/>
                <line x="10" y="100"/>
                <stroke/>
              </path>
              <path>
                <move x="90" y="0"/>
                <line x="90" y="100"/>
                <stroke/>
              </path>
            </foreground>
          </shape>

          <shape name="document" w="100" h="100" strokewidth="1">
            <foreground>
              <path>
                <move x="0" y="0"/>
                <line x="100" y="0"/>
                <line x="100" y="80"/>
                <curve x1="75" y1="90" x2="25" y2="70" x3="0" y3="80"/>
                <close/>
                <fillstroke/>
              </path>
            </foreground>
          </shape>

          <shape name="database" w="100" h="100" strokewidth="1">
            <foreground>
              <path>
                <move x="0" y="15"/>
                <curve x1="0" y1="33" x2="100" y2="33" x3="100" y3="15"/>
                <line x="100" y="85"/>
                <curve x1="100" y1="103" x2="0" y2="103" x3="0" y3="85"/>
                <close/>
                <fillstroke/>
              </path>
              <path>
                <move x="0" y="15"/>
                <curve x1="0" y1="-3" x2="100" y2="-3" x3="100" y3="15"/>
                <stroke/>
              </path>
            </foreground>
          </shape>

          <shape name="manual_input" w="100" h="100" strokewidth="1">
            <foreground>
              <path>
                <move x="0" y="25"/>
                <line x="100" y="0"/>
                <line x="100" y="100"/>
                <line x="0" y="100"/>
                <close/>
                <fillstroke/>
              </path>
            </foreground>
          </shape>

          <shape name="manual_operation" w="100" h="100" strokewidth="1">
            <foreground>
              <path>
                <move x="0" y="0"/>
                <line x="100" y="0"/>
                <line x="80" y="100"/>
                <line x="20" y="100"/>
                <close/>
                <fillstroke/>
              </path>
            </foreground>
          </shape>

          <shape name="preparation" w="100" h="100" strokewidth="1">
            <foreground>
              <path>
                <move x="20" y="0"/>
                <line x="80" y="0"/>
                <line x="100" y="50"/>
                <line x="80" y="100"/>
                <line x="20" y="100"/>
                <line x="0" y="50"/>
                <close/>
                <fillstroke/>
              </path>
            </foreground>
          </shape>

          <shape name="off_page_reference" w="100" h="100" strokewidth="1">
            <foreground>
              <path>
                <move x="0" y="0"/>
                <line x="100" y="0"/>
                <line x="100" y="65"/>
                <line x="50" y="100"/>
                <line x="0" y="65"/>
                <close/>
                <fillstroke/>
              </path>
            </foreground>
          </shape>

          <shape name="internal_storage" w="100" h="100" strokewidth="1">
            <foreground>
              <path>
                <move x="0" y="0"/>
                <line x="100" y="0"/>
                <line x="100" y="100"/>
                <line x="0" y="100"/>
                <close/>
                <fillstroke/>
              </path>
              <path>
                <move x="0" y="15"/>
                <line x="15" y="15"/>
                <line x="15" y="0"/>
                <stroke/>
              </path>
            </foreground>
          </shape>

          <shape name="delay" w="100" h="100" strokewidth="1">
            <foreground>
              <path>
                <move x="0" y="0"/>
                <line x="60" y="0"/>
                <curve x1="100" y1="0" x2="100" y2="100" x3="60" y3="100"/>
                <line x="0" y="100"/>
                <close/>
                <fillstroke/>
              </path>
            </foreground>
          </shape>

          <shape name="display" w="100" h="100" strokewidth="1">
            <foreground>
              <path>
                <move x="20" y="0"/>
                <line x="100" y="0"/>
                <curve x1="70" y1="25" x2="70" y2="75" x3="100" y3="100"/>
                <line x="20" y="100"/>
                <curve x1="50" y1="75" x2="50" y2="25" x3="20" y3="0"/>
                <close/>
                <fillstroke/>
              </path>
            </foreground>
          </shape>

          <shape name="stored_data" w="100" h="100" strokewidth="1">
            <foreground>
              <path>
                <move x="15" y="0"/>
                <line x="85" y="0"/>
                <curve x1="100" y1="0" x2="100" y2="100" x3="85" y3="100"/>
                <line x="15" y="100"/>
                <curve x1="0" y1="100" x2="0" y2="0" x3="15" y3="0"/>
                <close/>
                <fillstroke/>
              </path>
            </foreground>
          </shape>

          <shape name="loop_limit" w="100" h="100" strokewidth="1">
            <foreground>
              <path>
                <move x="0" y="20"/>
                <line x="20" y="0"/>
                <line x="80" y="0"/>
                <line x="100" y="20"/>
                <line x="100" y="100"/>
                <line x="0" y="100"/>
                <close/>
                <fillstroke/>
              </path>
            </foreground>
          </shape>

          <shape name="sequential_data" w="100" h="100" strokewidth="1">
            <foreground>
              <path>
                <move x="0" y="0"/>
                <line x="100" y="0"/>
                <line x="100" y="100"/>
                <line x="0" y="100"/>
                <close/>
                <fillstroke/>
              </path>
              <path>
                <move x="100" y="0"/>
                <curve x1="80" y1="33" x2="80" y2="67" x3="100" y3="100"/>
                <stroke/>
              </path>
            </foreground>
          </shape>

          <shape name="sort" w="100" h="100" strokewidth="1">
            <foreground>
              <path>
                <move x="0" y="0"/>
                <line x="50" y="50"/>
                <line x="100" y="0"/>
                <line x="100" y="100"/>
                <line x="50" y="50"/>
                <line x="0" y="100"/>
                <close/>
                <fillstroke/>
              </path>
            </foreground>
          </shape>
        </shapes>
        """;
}
